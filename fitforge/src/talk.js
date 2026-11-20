/* ============================== TALK TO BUILD ==============================
   Turns a typed or spoken request — "20 minutes, legs, no jumping, my knee's
   sore" — into a workout built from the app's own movement library.

   Deliberately on-device and deterministic:
   - it works offline and in the installed PWA, where the AI coach (which needs
     the Claude sandbox to reach the API) does not;
   - the same request always means the same thing, so what the screen says it
     heard is exactly what the builder used.

   Two pure steps, both testable without a browser:
     parseRequest(text, lib)            -> intent (what was asked for)
     buildTalkWorkout(intent, lib, ctx) -> { wod, notes } or a recovery suggestion
   plus mergeIntent(prev, next) so follow-ups ("harder", "swap the burpees",
   "make it shorter") adjust the previous request instead of starting over.

   `lib` is injected (EX, TECHNICAL, DIFFICULTY, scaleReps, …) so this file has
   no dependency on App.jsx and can't drift from the library it reads.
==================================================================== */

const LEVELS = ["easy", "moderate", "hard", "brutal"];
const MIN_MINUTES = 5;
const MAX_MINUTES = 60;

/* ---------------------------------------------------------------- text */

const NUM_WORDS = {
  one: 1, two: 2, three: 3, four: 4, five: 5, six: 6, seven: 7, eight: 8, nine: 9, ten: 10,
  eleven: 11, twelve: 12, thirteen: 13, fourteen: 14, fifteen: 15, sixteen: 16,
  seventeen: 17, eighteen: 18, nineteen: 19, twenty: 20, thirty: 30, forty: 40,
  fifty: 50, sixty: 60, ninety: 90,
};

export function normalize(raw) {
  let s = ` ${String(raw || "").toLowerCase()} `
    .replace(/[‘’ʼ]/g, "'")
    .replace(/[“”]/g, '"')
    .replace(/&/g, " and ")
    .replace(/(\d)\s*x\s*(\d)/g, "$1x$2")
    .replace(/-/g, " ")
    .replace(/[^a-z0-9'.,;:!?\s]/g, " ")
    // Apostrophes only matter inside words ("don't"); strip the rest.
    .replace(/(^|\s)'|'(\s|$)/g, "$1 $2")
    .replace(/\s+/g, " ");
  // "twenty five" -> 25, then single number words.
  s = s.replace(/\b(twenty|thirty|forty|fifty) (one|two|three|four|five|six|seven|eight|nine)\b/g,
    (_, t, u) => String(NUM_WORDS[t] + NUM_WORDS[u]));
  s = s.replace(/\b(one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty|fifty|sixty|ninety)\b/g,
    (w) => String(NUM_WORDS[w]));
  // Spoken durations people actually use.
  s = s
    .replace(/\b(an? )?hour and a half\b/g, " 90 minutes ")
    .replace(/\b(a )?quarter (of an )?hour\b/g, " 15 minutes ")
    .replace(/\bhalf (an )?hour\b/g, " 30 minutes ")
    .replace(/\b(an|1) (hour|hr)\b/g, " 60 minutes ")
    .replace(/\b(\d+(?:\.\d+)?) ?(hours|hour|hrs|hr|h)\b/g, (_, n) => ` ${Math.round(parseFloat(n) * 60)} minutes `)
    .replace(/\s+/g, " ");
  return s;
}

/* Replace a span with spaces so later passes can't match inside it, while
   keeping every index stable. */
function blank(text, start, end) {
  return text.slice(0, start) + " ".repeat(end - start) + text.slice(end);
}

function allMatches(re, text) {
  const flags = re.flags.includes("g") ? re.flags : re.flags + "g";
  const r = new RegExp(re.source, flags);
  const out = [];
  let m;
  while ((m = r.exec(text))) {
    out.push({ start: m.index, end: m.index + m[0].length, text: m[0], groups: m });
    if (m[0].length === 0) r.lastIndex++;
  }
  return out;
}

/* Negation lives in the words just before a mention, within the same clause.
   "No burpees, lunges or squats" negates the whole list; "no jumping, but
   include burpees" does not carry past the "but". */
const NEGATION = /\b(no|not|without|hate|hates|skip|avoid|except|minus|don'?t|do not|never|ban|swap(?: out)?|replace|drop|remove|lose|ditch|instead of|anything but|other than|can'?t|cannot|not a fan of|dislike|none of|nothing with|leave out|cut|get rid of|sick of|tired of)\b/;
const CLAUSE_BREAK = /[.;!?,:]|\bbut\b|\bthen\b|\bplus\b|\balso\b|\bwith\b/g;
const LIST_GLUE = /^[\s,]*(or|and|nor|and no|or no|and also no)?[\s,]*$/;

function clauseStartBefore(text, idx) {
  let last = 0;
  for (const m of allMatches(CLAUSE_BREAK, text)) {
    if (m.end <= idx) last = m.end; else break;
  }
  return last;
}

/* Walks a list of matches (sorted by position) and marks each as negated or
   not, carrying negation across simple lists. */
function markNegation(text, matches) {
  let prev = null;
  for (const m of matches) {
    const from = Math.max(prev ? prev.end : 0, clauseStartBefore(text, m.start));
    const gap = text.slice(from, m.start);
    const between = prev ? text.slice(prev.end, m.start) : null;
    if (NEGATION.test(gap)) m.negated = true;
    else if (prev && prev.negated && between !== null && LIST_GLUE.test(between)) m.negated = true;
    else {
      // "burpees are out", "lunges are off the table"
      const after = text.slice(m.end, m.end + 28);
      m.negated = /^\s*(are|is)?\s*(out|off( the table)?|banned|not allowed|a no|forbidden)\b/.test(after);
    }
    prev = m;
  }
  return matches;
}

/* ---------------------------------------------------------------- vocab */

const FOCUS = [
  { key: "full", label: "Full body", re: /\b(full ?body|total ?body|whole ?body|all over|everything)\b/, groups: ["Chest", "Back", "Shoulders", "Arms", "Quads", "Hamstrings", "Glutes", "Core"] },
  { key: "lower", label: "Legs", re: /\b(legs?|leg day|lower ?body|lower half|wheels)\b/, groups: ["Quads", "Hamstrings", "Glutes"] },
  { key: "upper", label: "Upper body", re: /\b(upper ?body|upper half)\b/, groups: ["Chest", "Back", "Shoulders", "Arms"] },
  { key: "push", label: "Push", re: /\b(push (day|session|workout|muscles)|pushing)\b/, patterns: ["press", "pushup"] },
  { key: "pull", label: "Pull", re: /\b(pull (day|session|workout|muscles)|pulling)\b/, patterns: ["pullup", "row", "curl"] },
  { key: "chest", label: "Chest", re: /\b(chest|pecs?)\b/, groups: ["Chest"] },
  { key: "back", label: "Back", re: /\b(back|lats?|upper back)\b/, groups: ["Back"] },
  { key: "shoulders", label: "Shoulders", re: /\b(shoulders?|delts?)\b/, groups: ["Shoulders"] },
  { key: "arms", label: "Arms", re: /\b(arms?|biceps?|triceps?|bis|tris|guns)\b/, groups: ["Arms"] },
  { key: "core", label: "Core", re: /\b(core|abs|ab|abdominals?|six ?pack|stomach|belly|midsection|obliques?|trunk)\b/, groups: ["Core"] },
  { key: "quads", label: "Quads", re: /\bquads?\b/, groups: ["Quads"] },
  { key: "hamstrings", label: "Hamstrings", re: /\b(hamstrings?|hammies|hams)\b/, groups: ["Hamstrings"] },
  { key: "glutes", label: "Glutes", re: /\b(glutes?|butt|booty|bum|backside)\b/, groups: ["Glutes"] },
  { key: "calves", label: "Calves", re: /\b(calf|calves)\b/, patterns: ["calf"] },
];

/* Phrases where a focus word isn't a focus at all. */
const FOCUS_NOISE = /\b(back to back|come back|coming back|get(ting)? back|back into|back in|back on|back off|back at it|bounce back|getting back|lay back|laid back|arms? length)\b/g;

const EQUIP_WORDS = [
  ["pull_up_bar", /\b(pull ?up bar|chin ?up bar|bar to hang( from)?|doorway bar)\b/],
  ["battle_rope", /\bbattle ?ropes?\b/],
  ["rope_to_climb", /\bclimbing rope\b/],
  ["dumbbells", /\b(dumb ?bells?|dbs?)\b/],
  ["kettlebell", /\b(kettle ?bells?|kbs?)\b/],
  ["barbell", /\b(barbells?|olympic bar|bar and plates|plates)\b/],
  ["squat_rack", /\b(squat rack|power rack|rack|cage)\b/],
  ["gymnastic_rings", /\b(gymnastic )?rings\b/],
  ["box", /\b(plyo )?box(es)?\b/],
  ["jump_rope", /\b(jump ?rope|skipping rope|skip rope|rope)\b/],
  ["bands", /\b(resistance )?bands?\b/],
  ["rowing_machine", /\b(rower|rowing machine|erg|concept ?2)\b/],
  ["assault_bike", /\b(assault ?bike|air ?bike|echo bike|spin bike|exercise bike|stationary bike|bike)\b/],
  ["cardio_machine", /\b(treadmill|elliptical|stair ?(climber|master|mill)|cross ?trainer)\b/],
  ["sandbag", /\bsand ?bags?\b/],
  ["sled", /\b(sled|prowler)\b/],
  ["wall_ball", /\b(wall ?balls?|med(icine)? ?balls?|slam ?balls?)\b/],
  ["bench", /\bbench\b/],
  ["cable_machine", /\b(cables?( machine)?|cable stack)\b/],
  ["dip_bars", /\b(dip (bars?|station)|parallel bars|parallettes)\b/],
  ["ab_wheel", /\bab (wheel|roller)\b/],
  ["leg_press", /\b(leg press|leg machine)\b/],
  ["pool", /\bpool\b/],
];

const OUTDOORS = /\b(outside|outdoors|park|track|field|garden|backyard|yard|beach|trail)\b/;
const BODYWEIGHT_ONLY = /\b(no (equipment|kit|gear|weights?)|without (any )?(equipment|weights?|kit|gear)|body ?weight( only)?|calisthenics|hotel( room)?|travell?ing|on the road|just my body|nothing but my body|no lifting)\b/;
const FULL_GYM = /\b(at the gym|in the gym|full gym|whole gym|gym)\b/;   // "home gym" is blanked first and means saved kit

/* Movement names. The library's own names are indexed automatically; these add
   what people actually say. A generic word ("squats", "rows") resolves to the
   best variant the person can do, chosen when the workout is built. */
const EXTRA_ALIASES = {
  pushup: ["press ups", "press up", "pressups"],
  kbswing: ["kb swings", "kb swing", "kettlebell swing", "russian swings", "american swings", "swings"],
  doubleunder: ["dus", "double unders", "double under"],
  toestobar: ["t2b", "ttb", "toes to bar"],
  hspu: ["hspu", "handstand push ups", "handstand pushups"],
  ohp: ["ohp", "shoulder press", "military press", "strict press"],
  bench: ["bench press", "bench pressing", "benching", "bench"],
  deadlift: ["deadlifts", "deads"],
  rdl: ["rdl", "rdls", "romanian deadlifts", "stiff leg deadlift", "stiff leg deadlifts"],
  plank: ["plank", "planks", "planking"],
  mountainclimber: ["mountain climbers", "mountain climber", "climbers"],
  farmerscarry: ["farmer carry", "farmers carry", "farmers walk", "farmer walk", "farmer carries", "farmers walks", "farmer s carry", "farmer s walk"],
  turkishgetup: ["tgu", "tgus", "get ups", "getups", "turkish get ups"],
  wallball: ["wall balls", "wallballs", "wall ball shots"],
  glutebridge: ["glute bridges", "bridges", "hip bridge", "hip bridges"],
  hipthrust: ["hip thrusts"],
  pullup: ["pull ups", "pullups", "chin ups", "chinups", "chin up"],
  muscleup: ["muscle ups", "muscleups"],
  snatch: ["snatches", "power snatches"],
  cleanjerk: ["clean and jerk", "clean and jerks", "cleans", "power cleans"],
  lateralraise: ["lateral raises", "side raises", "lat raises"],
  latpulldown: ["lat pulldowns", "pulldowns", "pull downs"],
  calfraise: ["calf raises"],
  deadbug: ["dead bugs", "deadbugs"],
  hollowhold: ["hollow holds", "hollow body", "hollow rocks"],
  medicineballslam: ["ball slams", "slams", "med ball slams"],
  battlerope: ["battle ropes", "battle rope", "rope waves"],
  sledpush: ["sled pushes", "prowler push"],
  jumpsquat: ["squat jumps"],
  jumpinglunge: ["lunge jumps", "jumping lunges"],
  pistolsquat: ["pistols"],
  jumprope: ["skipping", "jump rope", "jumping rope", "single unders", "singles"],
  briskwalk: ["walk", "walking", "incline walk", "hike", "hiking"],
  swimlaps: ["swim", "swimming", "laps"],
  burpee: ["burpee"],
  situp: ["sit up", "situps"],
  boxjump: ["box jump"],
};

const GENERIC_ALIASES = [
  { label: "Squats", words: ["squat", "squats"], ids: ["backsquat", "gobletsquat", "airsquat"] },
  { label: "Rows", words: ["rows", "row"], ids: ["barbellrow", "dbrow", "invertedrow", "renegaderow", "row"] },
  { label: "Curls", words: ["curl", "curls", "bicep curls", "biceps curls"], ids: ["curl", "hammercurl", "preachercurl"] },
  { label: "Dips", words: ["dip", "dips"], ids: ["tricepdip", "chestdips", "ringdip"] },
  { label: "Running", words: ["run", "runs", "running", "jog", "jogging", "sprint", "sprints", "sprinting"], ids: ["run400", "treadmillrun", "shuttlerun"] },
  { label: "Rowing", words: ["rowing", "row machine"], ids: ["row", "rowintervals"] },
  { label: "Bike", words: ["biking", "cycling", "spin"], ids: ["bike", "bikeintervals"] },
  { label: "Lunges", words: ["lunge", "lunges"], ids: ["lunge", "bulgariansplitsquat", "curtsylunge"] },
  { label: "Presses", words: ["overhead pressing", "pressing"], ids: ["ohp", "arnoldpress", "pikepushup"] },
  { label: "Jumping", words: ["jumping", "jumps", "plyo", "plyos", "plyometrics", "jump"], ids: null, patterns: ["jump"] },
];

function wordsOf(name) {
  return normalize(name.replace(/\(.*?\)/g, " ").replace(/\//g, " ")).trim();
}

function variantsOf(base) {
  const out = new Set([base]);
  const words = base.split(" ");
  const last = words[words.length - 1];
  const stem = words.slice(0, -1).join(" ");
  const join = (w) => (stem ? `${stem} ${w}` : w);
  if (/[^s]s$/.test(last)) out.add(join(last.slice(0, -1)));
  else if (!/s$/.test(last)) out.add(join(last + "s"));
  // "pull ups" / "pullups", "dead bug" / "deadbug"
  for (const v of [...out]) {
    const w = v.split(" ");
    if (w.length === 2 && w[1].length <= 4) out.add(w.join(""));
  }
  return [...out];
}

/* Built once per library. Sorted longest-first so "back squat" wins over
   "squat" and "romanian deadlift" over "deadlift". */
const aliasCache = new WeakMap();
function aliasIndex(lib) {
  if (aliasCache.has(lib.EX)) return aliasCache.get(lib.EX);
  const entries = [];
  const byId = Object.fromEntries(lib.EX.map((e) => [e.id, e]));
  // Generic words first so "row" or "run" means "the best variant you can do",
  // then hand-written aliases, then the library's own names.
  for (const g of GENERIC_ALIASES) {
    const ids = g.ids ? g.ids.filter((id) => byId[id]) : null;
    for (const w of g.words) entries.push({ alias: normalize(w).trim(), ids, patterns: g.patterns, label: g.label, generic: true });
  }
  for (const [id, words] of Object.entries(EXTRA_ALIASES)) {
    if (!byId[id]) continue;
    for (const w of words) entries.push({ alias: normalize(w).trim(), ids: [id], label: byId[id].name });
  }
  for (const e of lib.EX) {
    for (const v of variantsOf(wordsOf(e.name))) entries.push({ alias: v, ids: [e.id], label: e.name });
  }
  const seen = new Set();
  const uniq = entries.filter((e) => {
    if (!e.alias || seen.has(e.alias)) return false;
    seen.add(e.alias);
    return true;
  });
  uniq.sort((a, b) => b.alias.length - a.alias.length);
  const out = uniq.map((e) => ({ ...e, re: new RegExp(`\\b${e.alias.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}\\b`, "g") }));
  aliasCache.set(lib.EX, out);
  return out;
}

/* Body parts someone might be nursing. Each maps to what to leave out, and
   (where one exists) a yoga flow that helps. */
const BODY_PARTS = {
  knee: { re: "knees?", label: "knee", patterns: ["jump", "lunge"], ids: ["pistolsquat", "sissysquat", "run400", "shuttlerun", "treadmillrun", "boxstepover", "stepup", "jumpsquat", "jumpinglunge"], why: "jumping, lunging and running are out to spare your knee — squat only as deep as stays pain-free" },
  back: { re: "(lower )?back|spine", label: "back", patterns: ["hinge", "swing"], ids: ["backsquat", "frontsquat", "zerchersquat", "overheadsquat", "situp", "vup", "russiantwist", "yokecarry", "sandbagcarry", "barbellrow", "tbarrow", "burpee", "goodmorning", "rackpull"], yoga: "Lower Back", why: "no hinging, swinging or loaded spinal work while your back settles" },
  shoulder: { re: "shoulders?|rotator cuff", label: "shoulder", patterns: ["press", "pullup", "pushup"], ids: ["battlerope", "wallball", "snatch", "cleanjerk", "overheadsquat", "wallwalk", "lateralraise", "frontraise", "uprightrow", "burpee", "medicineballslam", "devilpress", "manmaker"], yoga: "Shoulders", why: "nothing pressing, pulling or overhead for your shoulder" },
  wrist: { re: "wrists?", label: "wrist", patterns: [], ids: ["pushup", "burpee", "mountainclimber", "bearcrawl", "hspu", "pikepushup", "wallwalk", "frontsquat", "cleanjerk", "snatch", "renegaderow", "devilpress", "manmaker", "thruster", "overheadsquat", "snatchbalance", "dballclean", "bench", "inclinebench", "declinebench", "closegripbench"], why: "nothing that loads your wrist in extension" },
  ankle: { re: "ankles?|achilles", label: "ankle", patterns: ["jump"], ids: ["run400", "shuttlerun", "treadmillrun", "stairclimber", "sledpush", "sleddrag", "calfraise", "lunge", "jumpinglunge"], why: "no jumping, running or calf work for your ankle" },
  elbow: { re: "elbows?|tennis elbow|golfer'?s elbow", label: "elbow", patterns: ["curl"], ids: ["skullcrusher", "overheadtricep", "cablepushdown", "ringdip", "chestdips", "tricepdip", "closegripbench", "hspu", "pullup", "muscleup", "barmuscleup"], why: "no curling, dipping or heavy elbow flexion" },
  neck: { re: "neck", label: "neck", patterns: [], ids: ["hspu", "wallwalk", "situp", "vup", "shrugs", "backsquat", "yokecarry", "zerchersquat"], yoga: "Neck", why: "nothing loading or flexing your neck" },
  hip: { re: "hips?|groin|hip flexors?", label: "hip", patterns: [], ids: ["pistolsquat", "lunge", "jumpinglunge", "curtsylunge", "bulgariansplitsquat", "sumosquat", "hipabduction", "bandedwalk", "boxjump", "broadjump", "tuckjump", "run400", "shuttlerun"], yoga: "Hips", why: "no deep single-leg or wide-stance work for your hip" },
  hamstring: { re: "hamstrings?|hammies", label: "hamstring", groups: ["Hamstrings"], patterns: [], ids: ["nordiccurl", "shuttlerun", "run400", "rdl", "singlelegrdl", "deadlift", "goodmorning", "kbswing"], yoga: "Hamstrings", why: "no hamstring loading or sprinting" },
  legs: { re: "legs?|quads?|calves|glutes?", label: "legs", groups: ["Quads", "Hamstrings", "Glutes"], patterns: ["jump", "lunge", "calf"], ids: ["run400", "shuttlerun"], why: "legs are resting today" },
  arms: { re: "arms?|biceps?|triceps?|forearms?", label: "arms", groups: ["Arms"], patterns: ["curl"], ids: [], why: "arms are resting today" },
  chest: { re: "pecs?", label: "chest", groups: ["Chest"], patterns: ["pushup"], ids: [], why: "chest is resting today" },
  core: { re: "abs|core|stomach", label: "core", groups: ["Core"], patterns: ["core", "plank"], ids: [], why: "core is resting today" },
};

const PAIN = "(bad|sore|hurt|hurts|hurting|injured|injury|tweaked|pain|painful|achy|aching|aches|ache|stiff|dodgy|tender|recovering|rehab|rehabbing|niggle|niggly|torn|sprained|strained|pulled|tight|wrecked|fried|dead|toast|smoked|shot|cranky|killing me|playing up|acting up|not great|messed up|banged up|beat up|surgery|operation)";
const PROTECT = "(protect|careful with|easy on|go easy on|spare|mind|baby|look after|nurse|rest|resting|avoid|skip|no|not|without)";

/* "Chest pain" from a workout plan is not DOMS. These stop the builder and
   say so rather than handing over a session. */
const RED_FLAGS = /\b(chest pains?|chest (is |feels )?tight(ness)?|tight(ness)? in (my|the) chest|pain in (my|the) chest|dizz(y|iness)|faint(ed|ing)?|light ?headed|palpitations?|heart (is |was )?(racing|pounding|hurts?|pain|fluttering|skipping)|short(ness)? of breath|can'?t breathe|numb(ness)? (in|down) (my )?(left )?arm)\b/;

/* ---------------------------------------------------------------- parse */

export function emptyIntent() {
  return {
    texts: [],
    minutes: null,
    level: null,
    format: null,
    goal: null,          // "strength" | "muscle" | null
    focus: [],           // FOCUS keys
    cardio: false,
    noCardio: false,
    equipment: [],       // ids named by the person
    noEquipment: [],     // ids they said they don't have / don't want
    bodyweightOnly: false,
    outdoors: false,
    fullGym: false,
    include: [],         // [{ label, ids }]
    exclude: [],         // [{ label, ids, patterns }]
    avoid: [],           // BODY_PARTS keys
    avoidFocus: [],      // FOCUS keys the person said no to
    recovery: null,      // { yoga, breath } when the ask is to wind down / stretch
    wantsWorkout: false,
    redFlag: false,
    pregnant: false,
  };
}

const RECOVERY_YOGA = /\b(yoga|stretch(es|ing)?|mobility|flexib(le|ility)|loosen( up)?|limber|flow|cool ?down|foam roll(ing)?|unwind)\b/;
const RECOVERY_BREATH = /\b(breath(e|ing|work)?|calm( down)?|anxious|anxiety|stress(ed|ful)?|panic(ky)?|relax(ing)?|wind down|sleep|bed ?time|meditat(e|ion)|centre myself|center myself)\b/;
const WORKOUT_WORDS = /\b(work ?out|session|wod|train(ing)?|exercise|lift(ing)?|circuit|sweat|burn|hiit|metcon|conditioning|cardio|strength|blast|smash|routine|amrap|emom|tabata|chipper|for time|reps?|sets?)\b/;

export function parseRequest(raw, lib) {
  const intent = emptyIntent();
  intent.texts = [String(raw || "").trim()].filter(Boolean);
  const full = normalize(raw);       // untouched, for negation look-backs
  let text = full;                   // progressively blanked as phrases are claimed
  const delta = { levelStep: 0, minutesStep: 0, reroll: false, reset: false };

  if (RED_FLAGS.test(full)) intent.redFlag = true;
  if (/\bpregnan(t|cy)\b|\bpost ?partum\b/.test(full)) intent.pregnant = true;

  /* Red-flag and breath phrases are blanked so "chest" and "box" in them
     don't get read as a focus or a piece of kit. */
  for (const m of allMatches(RED_FLAGS, text)) text = blank(text, m.start, m.end);
  for (const m of allMatches(/\bbox breathing\b/, text)) text = blank(text, m.start, m.end);
  for (const m of allMatches(/\bhome gym\b/, text)) text = blank(text, m.start, m.end);
  // "after my run", "before a game" describe the day, they aren't requests.
  const CONTEXT = /\b(after|before|post|pre|from|since) (a |an |my |the |this |today'?s |yesterday'?s |last night'?s )?(long |hard |big |easy )?(run|ride|workout|session|game|match|lift|swim|hike|class|shift|flight|race|leg day)\b/g;
  for (const m of allMatches(CONTEXT, text)) text = blank(text, m.start, m.end);
  const askText = text;   // the request without its backstory
  // "a bench" / "my bench" is kit; a bare "bench" next to lifts is bench press.
  for (const m of allMatches(/\b(a|an|the|my|flat|adjustable|incline|weight|weights|and) bench\b(?! press)/, text)) {
    if (/^and /.test(m.text) && !/\b(dumb ?bells?|kettle ?bells?|barbell|have|got)\b/.test(full.slice(Math.max(0, m.start - 30), m.start))) continue;
    const [mark] = markNegation(full, [{ start: m.start, end: m.end }]);
    const list = mark.negated ? intent.noEquipment : intent.equipment;
    if (!list.includes("bench")) list.push("bench");
    text = blank(text, m.start, m.end);
  }

  /* 1. Movements, longest alias first. Each match claims its span, so the
     passes below can't read "dead bugs" as a dead core or "rack pull" as a rack. */
  const found = [];
  for (const a of aliasIndex(lib)) {
    for (const m of allMatches(a.re, text)) {
      found.push({ ...m, alias: a });
      text = blank(text, m.start, m.end);
    }
  }
  found.sort((a, b) => a.start - b.start);
  markNegation(full, found);
  for (const f of found) {
    const entry = { label: f.alias.label, ids: f.alias.ids, patterns: f.alias.patterns || null, generic: !!f.alias.generic };
    if (f.negated) intent.exclude.push(entry);
    else if (entry.ids) intent.include.push(entry);
    // A positive "jumping" is a preference for conditioning, not a movement to force in.
    else if (entry.patterns) intent.cardio = true;
  }

  /* 2. Injuries and sore spots: "my knee hurts", "bad back", "go easy on my
     shoulder". Claimed before intensity so "easy on my knee" isn't "easy". */
  for (const [key, part] of Object.entries(BODY_PARTS)) {
    const P = `(?:${part.re})`;
    const res = [
      new RegExp(`\\b${PAIN}\\b(?:\\s+[a-z']+){0,3}?\\s+${P}\\b`, "g"),
      new RegExp(`\\b${P}\\b(?:\\s+[a-z']+){0,3}?\\s+${PAIN}\\b`, "g"),
      new RegExp(`\\b${PROTECT}\\s+(?:my\\s+|the\\s+|your\\s+)?${P}\\b`, "g"),
    ];
    let hit = false;
    for (const re of res) {
      for (const m of allMatches(re, text)) {
        // "no pain in my knee" or "knee feels fine" is not an injury.
        if (/\bno pain\b|\bfeels? (fine|good|great|ok|okay)\b/.test(m.text)) continue;
        hit = true;
        text = blank(text, m.start, m.end);
      }
    }
    if (hit && !intent.avoid.includes(key)) intent.avoid.push(key);
  }

  /* 3. Recovery asks: stretching, yoga, breathing, sleep. */
  const yogaHit = RECOVERY_YOGA.test(text);
  const breathHit = RECOVERY_BREATH.test(text);
  if (yogaHit || breathHit) {
    intent.recovery = { yoga: yogaHit ? yogaAreaFor(full, intent.avoid) : null, breath: breathHit ? breathFor(full) : null };
    if (!intent.recovery.yoga && /\b(relax|unwind|wind down|sleep|bed)/.test(full)) intent.recovery.yoga = "Relaxation";
  }

  /* 4. Follow-up modifiers. On a fresh request they're harmless — "harder"
     just means hard — but after a reply they move the previous workout. */
  if (/\b(start over|start again|reset|scratch that|forget (that|it)|new request|clear it)\b/.test(text)) delta.reset = true;
  if (/\b(another( one)?|different( one)?|again|shuffle|re ?roll|new one|something else|try again|not that one|other options?|mix it up)\b/.test(text)) delta.reroll = true;
  if (/\b(harder|tougher|more intense|step it up|more challenging|kick it up|crank it up|too easy|push me more)\b/.test(text)) delta.levelStep = 1;
  if (/\b(easier|lighter|gentler|less intense|tone it down|dial it back|too hard|too much|too intense|bit less)\b/.test(text)) delta.levelStep = -1;
  if (/\b(shorter|less time|quicker|too long|cut it down|trim it)\b/.test(text)) delta.minutesStep = -1;
  if (/\b(longer|more time|too short|extend it)\b/.test(text)) delta.minutesStep = 1;

  /* 5. Duration. Explicit minutes beat words like "quick". */
  const mins = text.match(/\b(\d{1,3})(?: ?(?:to|or)? ?\d{1,3})? ?(min|mins|minute|minutes|m)\b/);
  if (mins) intent.minutes = Number(mins[1]);
  else if (/\b(quick|short|fast|brief|little|tiny|mini|lunch ?break)\b/.test(text) && !delta.minutesStep) intent.minutes = 10;
  else if (/\b(long|big|epic|marathon)\b/.test(text) && !/\b(so long|too long|long time)\b/.test(text)) intent.minutes = 45;
  intent.minutesClamped = null;
  if (intent.minutes !== null) {
    if (intent.minutes > MAX_MINUTES || intent.minutes < MIN_MINUTES) intent.minutesClamped = intent.minutes;
    intent.minutes = Math.min(MAX_MINUTES, Math.max(MIN_MINUTES, intent.minutes));
  }

  /* 6. Intensity. Negated "hard" ("not too hard") reads as easy. */
  if (/\b(not too (hard|intense|tough|heavy|crazy)|nothing (too )?(crazy|intense|hard|heavy)|don'?t want to die|take it easy)\b/.test(text)) intent.level = "easy";
  else if (/\b(brutal|destroy me|wreck me|smash me|punish(ing)?|insane|max effort|all ?out|redline|savage|hardest|killer|crush me)\b/.test(text)) intent.level = "brutal";
  else if (/\b(hard|tough|intense|challenging|push me|sweaty|kick my (butt|ass)|beast mode|spicy|go hard|serious|harder)\b/.test(text)) intent.level = "hard";
  else if (/\b(easy|easier|light|gentle|chill|relaxed|low (intensity|key|effort)|beginners?|newbie|deload|recovery|tired|exhausted|knackered|wiped|low energy|hungover|rusty|getting back into|first time|haven'?t (worked out|trained|exercised)|out of shape|unfit|sore|achy)\b/.test(text)) intent.level = "easy";
  else if (/\b(moderate|medium|normal|standard|average|regular|middle)\b/.test(text)) intent.level = "moderate";

  /* 7. Format and goal. */
  if (/\bamrap\b|as many rounds/.test(text)) intent.format = "AMRAP";
  else if (/\bemom\b|every minute|on the minute|\bintervals?\b/.test(text)) intent.format = "EMOM";
  else if (/\btabata\b/.test(text)) intent.format = "Tabata";
  else if (/\bchipper\b/.test(text)) intent.format = "Chipper";
  else if (/\b(for time|rft|rounds for time|circuits?)\b/.test(text)) intent.format = "RFT";

  if (/\b(strength|strong(er)?|heavy|lift heavy|max(es)?|1 ?rm|one rep max|powerlifting|5x5|low reps?)\b/.test(text)) intent.goal = "strength";
  else if (/\b(build muscle|muscle building|hypertrophy|bodybuilding|pump|tone|toning|size|bulk|gains)\b/.test(text)) intent.goal = "muscle";
  if (intent.goal && !intent.format) intent.format = "Strength";

  if (/\b(no|not|without|skip|hate) (any )?(cardio|conditioning)\b/.test(text)) intent.noCardio = true;
  else if (/\b(cardio|conditioning|endurance|stamina|engine|sweat(y)?|heart rate|fat ?burn(ing)?|burn calories|lungs|hiit|metcon|aerobic|get fitter|fitness|out of breath)\b/.test(text)) intent.cardio = true;

  /* 8. Blanket exclusions. */
  if (/\b(low impact|no impact|quiet|apartment|flat|downstairs|neighbou?rs|sleeping baby|kids? (are )?asleep)\b/.test(text)) {
    intent.exclude.push({ label: "Jumping & impact", ids: ["burpee", "run400", "shuttlerun", "treadmillrun", "tuckjump", "broadjump", "jumpsquat", "jumpinglunge", "boxjump", "medicineballslam", "jumprope", "doubleunder"], patterns: ["jump"] });
  }
  if (/\b(no|not|without|avoid) (anything )?overhead\b/.test(text)) {
    intent.exclude.push({ label: "Overhead work", ids: ["ohp", "pushpress", "pushjerk", "thruster", "wallball", "hspu", "snatch", "overheadsquat", "wallwalk", "arnoldpress", "overheadtricep", "snatchbalance", "cleanjerk", "pikepushup", "turkishgetup"], patterns: null });
  }
  if (/\b(no|can'?t get (down )?on|avoid|not on) (the )?floor\b|\bno floor work\b|\bstanding only\b/.test(text)) {
    intent.exclude.push({ label: "Floor work", ids: ["burpee", "pushup", "plank", "sideplank", "mountainclimber", "situp", "vup", "deadbug", "hollowhold", "superman", "russiantwist", "glutebridge", "singlelegbridge", "frogpump", "bearcrawl", "pikepushup", "hspu", "turkishgetup", "renegaderow", "devilpress", "manmaker", "abwheelrollout"], patterns: ["plank"] });
  }

  /* 9. Equipment. Negated mentions ("no dumbbells", "don't have a bench") are
     removed even if they're saved in the equipment list. */
  if (BODYWEIGHT_ONLY.test(text)) intent.bodyweightOnly = true;
  for (const m of allMatches(BODYWEIGHT_ONLY, text)) text = blank(text, m.start, m.end);
  const equipFound = [];
  for (const [id, re] of EQUIP_WORDS) {
    for (const m of allMatches(re, text)) {
      equipFound.push({ ...m, id });
      text = blank(text, m.start, m.end);
    }
  }
  equipFound.sort((a, b) => a.start - b.start);
  markNegation(full, equipFound);
  for (const e of equipFound) {
    if (e.negated) { if (!intent.noEquipment.includes(e.id)) intent.noEquipment.push(e.id); }
    else if (!intent.equipment.includes(e.id)) intent.equipment.push(e.id);
  }
  if (intent.equipment.length) intent.bodyweightOnly = false;
  if (OUTDOORS.test(text)) intent.outdoors = true;
  if (FULL_GYM.test(text)) intent.fullGym = true;

  /* 10. Focus, from what's left. "No legs today" is an avoid, not a focus. */
  for (const m of allMatches(FOCUS_NOISE, text)) text = blank(text, m.start, m.end);
  const focusFound = [];
  for (const f of FOCUS) {
    for (const m of allMatches(f.re, text)) {
      focusFound.push({ ...m, key: f.key });
      text = blank(text, m.start, m.end);
    }
  }
  focusFound.sort((a, b) => a.start - b.start);
  markNegation(full, focusFound);
  for (const f of focusFound) {
    const list = f.negated ? intent.avoidFocus : intent.focus;
    if (!list.includes(f.key)) list.push(f.key);
  }
  intent.focus = intent.focus.filter((k) => !intent.avoidFocus.includes(k));

  intent.wantsWorkout = WORKOUT_WORDS.test(askText)
    || intent.focus.length > 0 || intent.include.length > 0 || !!intent.format || !!intent.goal || intent.cardio
    || intent.equipment.length > 0 || intent.bodyweightOnly;

  intent.delta = delta;
  intent.understood = understoodAnything(intent) || delta.levelStep !== 0 || delta.minutesStep !== 0 || delta.reroll || delta.reset;
  return intent;
}

function understoodAnything(i) {
  return !!(i.minutes || i.level || i.format || i.goal || i.focus.length || i.cardio || i.noCardio || i.equipment.length
    || i.noEquipment.length || i.bodyweightOnly || i.fullGym || i.include.length || i.exclude.length || i.avoid.length
    || i.avoidFocus.length || i.recovery || i.redFlag || i.wantsWorkout);
}

function yogaAreaFor(text, avoid) {
  if (/\bhips?|hip flexors?\b/.test(text) || avoid.includes("hip")) return "Hips";
  if (/\blower back|\bback\b/.test(text) || avoid.includes("back")) return "Lower Back";
  if (/\bneck\b/.test(text) || avoid.includes("neck")) return "Neck";
  if (/\bshoulders?\b/.test(text) || avoid.includes("shoulder")) return "Shoulders";
  if (/\bhamstrings?|hammies\b/.test(text) || avoid.includes("hamstring")) return "Hamstrings";
  if (/\bsplits?\b/.test(text)) return "Splits";
  if (/\b(morning|wake up|waking up|stiff)\b/.test(text)) return "Wake Up";
  if (/\b(sleep|bed|relax|unwind|wind down|calm)\b/.test(text)) return "Relaxation";
  if (/\bbalance\b/.test(text)) return "Core & Balance";
  if (/\bbackbends?\b/.test(text)) return "Backbends";
  return "Full Body Flow";
}

function breathFor(text) {
  if (/\b(sleep|bed ?time|wind down|insomnia)\b/.test(text)) return "478";
  if (/\b(anxious|anxiety|panic|stress|stressed|stressful|overwhelmed|calm)\b/.test(text)) return "physio";
  if (/\b(focus|pre ?workout|before (a|my) (lift|workout|session|game|race)|nervous|energy)\b/.test(text)) return "box";
  return "coherent";
}

/* ---------------------------------------------------------------- merge */

/* A follow-up refines the request in front of it. Explicit new values win,
   lists accumulate, and a movement you now include is no longer excluded (and
   vice versa). "Start over" drops the old request entirely. */
export function mergeIntent(prev, next) {
  if (!prev || next.delta?.reset) return { ...next, delta: { ...next.delta, reset: false } };
  const out = { ...prev, delta: next.delta };
  out.texts = [...prev.texts, ...next.texts];

  out.minutesClamped = next.minutesClamped ?? null;
  if (next.minutes) out.minutes = next.minutes;
  else if (next.delta.minutesStep) {
    const base = prev.minutes || 20;
    const step = base <= 10 ? 5 : 10;
    out.minutes = Math.min(MAX_MINUTES, Math.max(MIN_MINUTES, base + step * next.delta.minutesStep));
  }

  if (next.delta.levelStep) {
    const base = LEVELS.indexOf(prev.level || "moderate");
    out.level = LEVELS[Math.min(3, Math.max(0, base + next.delta.levelStep))];
  } else if (next.level) out.level = next.level;

  if (next.format) out.format = next.format;
  if (next.goal) out.goal = next.goal;
  if (next.focus.length) out.focus = next.focus;
  if (next.cardio) { out.cardio = true; out.noCardio = false; }
  if (next.noCardio) { out.noCardio = true; out.cardio = false; }

  if (next.bodyweightOnly) { out.bodyweightOnly = true; out.equipment = []; out.fullGym = false; }
  if (next.equipment.length) { out.equipment = next.equipment; out.bodyweightOnly = false; }
  if (next.fullGym) { out.fullGym = true; out.bodyweightOnly = false; }
  if (next.outdoors) out.outdoors = true;
  out.noEquipment = union(prev.noEquipment, next.noEquipment);
  out.equipment = out.equipment.filter((e) => !out.noEquipment.includes(e));

  const key = (e) => (e.ids || e.patterns || []).join(",");
  const newIncl = new Set(next.include.map(key));
  const newExcl = new Set(next.exclude.map(key));
  out.include = [...prev.include.filter((e) => !newExcl.has(key(e))), ...next.include.filter((e) => !prev.include.some((p) => key(p) === key(e)))];
  out.exclude = [...prev.exclude.filter((e) => !newIncl.has(key(e))), ...next.exclude.filter((e) => !prev.exclude.some((p) => key(p) === key(e)))];

  out.avoid = union(prev.avoid, next.avoid);
  out.avoidFocus = union(prev.avoidFocus.filter((f) => !next.focus.includes(f)), next.avoidFocus);
  out.focus = out.focus.filter((f) => !out.avoidFocus.includes(f));
  if (next.recovery) out.recovery = next.recovery;
  out.wantsWorkout = prev.wantsWorkout || next.wantsWorkout;
  out.redFlag = next.redFlag;
  out.pregnant = prev.pregnant || next.pregnant;
  out.understood = true;
  return out;
}

function union(a = [], b = []) {
  return [...new Set([...a, ...b])];
}

/* ---------------------------------------------------------------- build */

function pickFrom(list, rng) {
  return list[Math.floor(rng() * list.length)];
}

function shuffle(list, rng) {
  const a = [...list];
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(rng() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

function tidy(n) {
  if (n >= 20) return Math.round(n / 5) * 5;
  if (n >= 6) return Math.round(n / 2) * 2;
  return Math.max(1, Math.round(n));
}

/* Rough seconds per unit, used only to size rounds and chippers so the
   session lands near the time asked for. */
function secPerUnit(ex) {
  if (ex.unit === "cal") return 4;
  if (ex.unit === "m") return ex.pattern === "carry" ? 0.8 : 0.3;
  if (ex.unit === "sec") return 1;
  if (ex.unit === "min") return 60;
  return ex.pattern === "jump" ? 2.5 : ["hinge", "squat", "press"].includes(ex.pattern) && ex.prop ? 3.5 : 3;
}

function prescribe(ex, base, level, lib) {
  const vol = lib.DIFFICULTY[level].vol;
  switch (ex.unit) {
    case "cal": return { reps: `${lib.scaleReps(lib.jitter(base.cal), level)} cal`, sec: 0 };
    case "m": {
      if (ex.pattern === "carry") {
        // Carries and crawls are short: 20m, not the 100m floor runs get.
        const metres = Math.max(10, Math.round((lib.jitter(base.carry) * vol) / 10) * 10);
        return { reps: `${metres}m`, sec: 0 };
      }
      return { reps: `${lib.scaleDistance(lib.jitter(base.m), level)}m`, sec: 0 };
    }
    case "sec": {
      const s = Math.min(90, Math.max(15, Math.round((base.sec * Math.sqrt(vol)) / 5) * 5));
      return { reps: `${s}s`, sec: 0 };
    }
    case "min": {
      const s = Math.min(240, Math.max(30, Math.round((base.sec * 2 * Math.sqrt(vol)) / 15) * 15));
      return { reps: s >= 60 && s % 60 === 0 ? `${s / 60} min` : `${s}s`, sec: 0 };
    }
    default: return { reps: `${lib.scaleReps(lib.jitter(base.reps), level)}`, sec: 0 };
  }
}

function seconds(ex, reps) {
  const n = parseFloat(String(reps)) || 0;
  if (/min$/.test(reps)) return n * 60;
  if (/s$/.test(reps) && !/cal/.test(reps)) return n;
  if (/m$/.test(reps)) return n * secPerUnit(ex);
  return n * secPerUnit(ex);
}

function equipmentSet(intent, ctx, lib) {
  const all = lib.EQUIPMENT ? lib.EQUIPMENT.map((e) => e.id) : [];
  let set;
  let source;
  if (intent.bodyweightOnly) {
    set = new Set(["bodyweight"]);
    source = "bodyweight";
  } else if (intent.equipment.length && !intent.fullGym) {
    set = new Set(["bodyweight", ...intent.equipment]);
    // Dumbbells and a bench go together often enough that naming one shouldn't
    // strand the other, but nothing else is assumed.
    source = "named";
  } else if (intent.fullGym) {
    set = new Set(all.filter((id) => id !== "pool"));
    source = "gym";
  } else {
    set = new Set(["bodyweight", ...(ctx.availEquip || [])]);
    source = "saved";
  }
  if (intent.outdoors) set.add("space_to_run");
  for (const id of intent.noEquipment) set.delete(id);
  set.add("bodyweight");
  return { set, source };
}

/* The library lists equipment as alternatives ("dumbbells or barbell"), but a
   bench or a rack only supports a lift — it doesn't make a barbell appear. So
   Decline Bench Press needs the barbell; Dumbbell Row is fine with dumbbells. */
const SUPPORT_ONLY = new Set(["bench", "squat_rack"]);
const SUPPORT_IS_ENOUGH = { tricepdip: "bench", invertedrow: "squat_rack", stepup: "bench" };
function isUsable(ex, equip) {
  if (ex.equip.includes("bodyweight")) return true;
  if (SUPPORT_IS_ENOUGH[ex.id] && equip.has(SUPPORT_IS_ENOUGH[ex.id])) return true;
  const primary = ex.equip.filter((q) => !SUPPORT_ONLY.has(q));
  if (!primary.length) return ex.equip.some((q) => equip.has(q));
  return primary.some((q) => equip.has(q));
}

function excludedIds(intent) {
  const ids = new Set();
  const patterns = new Set();
  const groups = new Set();
  for (const e of intent.exclude) {
    (e.ids || []).forEach((id) => ids.add(id));
    (e.patterns || []).forEach((p) => patterns.add(p));
  }
  for (const key of intent.avoid) {
    const part = BODY_PARTS[key];
    part.ids.forEach((id) => ids.add(id));
    part.patterns.forEach((p) => patterns.add(p));
    (part.groups || []).forEach((g) => groups.add(g));
  }
  for (const key of intent.avoidFocus) {
    const f = FOCUS.find((x) => x.key === key);
    (f.groups || []).forEach((g) => groups.add(g));
    (f.patterns || []).forEach((p) => patterns.add(p));
  }
  if (intent.noCardio) patterns.add("run");
  return { ids, patterns, groups };
}

function avoidsGroups(ex, groups) {
  if (!groups.size) return false;
  if (groups.has(ex.muscle[0])) return true;
  const specific = ex.muscle.filter((m) => m !== "Full Body");
  return specific.length > 0 && specific.every((m) => groups.has(m));
}

function focusMatcher(intent) {
  const defs = intent.focus.map((k) => FOCUS.find((f) => f.key === k)).filter(Boolean);
  if (!defs.length) return null;
  return defs.map((f) => ({
    label: f.label,
    test: (ex) => (f.groups ? ex.muscle.some((m) => f.groups.includes(m)) : f.patterns.includes(ex.pattern)),
  }));
}

function isCardioish(ex) {
  return !!ex.cardio || ["jump", "run", "swing"].includes(ex.pattern) || ["burpee", "mountainclimber", "thruster", "wallball", "devilpress", "manmaker", "bearcrawl", "medicineballslam"].includes(ex.id);
}

const FORMAT_LABEL = { AMRAP: "AMRAP", EMOM: "EMOM", RFT: "For time", Tabata: "Tabata", Chipper: "Chipper", Strength: "Strength" };

/* Picks a format when the person didn't name one. Short sessions favour
   formats that are honest at that length: a 6-minute chipper is a warm-up. */
function chooseFormat(intent, minutes, rng) {
  if (intent.format) return intent.format;
  if (intent.goal) return "Strength";
  if (minutes <= 8) return pickFrom(["Tabata", "EMOM", "AMRAP"], rng);
  if (intent.cardio) return pickFrom(minutes <= 24 ? ["AMRAP", "EMOM", "Tabata", "RFT"] : ["AMRAP", "EMOM", "RFT"], rng);
  const bag = ["AMRAP", "AMRAP", "EMOM", "RFT"];
  if (minutes >= 20) bag.push("Chipper");
  return pickFrom(bag, rng);
}

function defaultLevel(ctx) {
  const lvl = ctx.settings?.level;
  if (lvl === "beginner") return "easy";
  if (lvl === "advanced") return "hard";
  return "moderate";
}

/* Chooses `n` movements. Named movements go in first; the rest are drawn
   round-robin across the focus areas so "legs and core" gets both, from
   movements using the kit the person named where possible. */
function selectMovements({ pool, n, include, focus, preferEquip, cardio, rng, technical }) {
  const chosen = [...include];
  const has = new Set(chosen.map((e) => e.id));
  const rank = (list) => {
    const sh = shuffle(list, rng);
    const score = (ex) => (preferEquip.size && ex.equip.some((eq) => preferEquip.has(eq)) ? 2 : 0)
      + (cardio && isCardioish(ex) ? 1 : 0)
      // High-skill moves only when the session is pitched for them.
      - (technical && technical.has(ex.id) ? 1.5 : 0);
    return sh.sort((a, b) => score(b) - score(a));
  };
  const lanes = focus || [{ label: null, test: () => true }];
  let lane = chosen.length % lanes.length;
  let guard = 0;
  while (chosen.length < n && guard++ < 200) {
    const want = lanes[lane % lanes.length];
    lane++;
    const options = rank(pool.filter((ex) => !has.has(ex.id) && want.test(ex)));
    // Avoid two near-identical moves (same pattern) back to back when there's a choice.
    const prevPattern = chosen[chosen.length - 1]?.pattern;
    const pickEx = options.find((ex) => ex.pattern !== prevPattern) || options[0];
    if (!pickEx) {
      if (lanes.every((l) => !pool.some((ex) => !has.has(ex.id) && l.test(ex)))) break;
      continue;
    }
    chosen.push(pickEx);
    has.add(pickEx.id);
  }
  return chosen;
}

export function buildTalkWorkout(intent, lib, ctx = {}) {
  const rng = ctx.rng || Math.random;
  const notes = [];

  if (intent.redFlag) {
    return {
      kind: "stop",
      say: "I'm not going to build a workout around that. Chest pain, dizziness, a racing heart or trouble breathing need checking by a doctor first — if it's happening now or it's severe, call emergency services.",
    };
  }

  // Wind-down asks with no workout in them get a recovery suggestion.
  if (intent.recovery && !intent.wantsWorkout) {
    const bits = [];
    if (intent.recovery.yoga) bits.push(`the ${intent.recovery.yoga} yoga flow`);
    const bp = intent.recovery.breath && lib.BREATH_PATTERNS?.find((p) => p.id === intent.recovery.breath);
    if (bp) bits.push(bp.name);
    return {
      kind: "recovery",
      recovery: { yoga: intent.recovery.yoga, breath: bp ? { id: bp.id, name: bp.name, tag: bp.tag } : null },
      say: `For that I'd skip the workout and do ${bits.join(", then ")}.`,
    };
  }

  const level = intent.pregnant && intent.level && LEVELS.indexOf(intent.level) > 1 ? "moderate" : intent.level || defaultLevel(ctx);
  const D = lib.DIFFICULTY[level];
  const minutes = intent.minutes || (intent.goal ? 40 : 20);
  if (intent.minutesClamped) {
    notes.push(intent.minutesClamped > MAX_MINUTES
      ? `Capped at ${MAX_MINUTES} minutes — past that, split it into two sessions.`
      : `${MIN_MINUTES} minutes is the shortest I'll build.`);
  }
  if (intent.pregnant) notes.push("Kept it moderate with nothing high-impact. Follow your midwife or doctor's guidance over anything here.");

  const { set: equip, source } = equipmentSet(intent, ctx, lib);
  const ex = excludedIds(intent);
  const EX_MAP = Object.fromEntries(lib.EX.map((e) => [e.id, e]));

  const usable = (e) => isUsable(e, equip);
  const allowed = (e) => !ex.ids.has(e.id) && !ex.patterns.has(e.pattern) && !avoidsGroups(e, ex.groups);
  let pool = lib.EX.filter((e) => usable(e) && allowed(e));
  if (intent.pregnant) pool = pool.filter((e) => e.pattern !== "jump" && !["situp", "vup", "hollowhold", "russiantwist", "burpee"].includes(e.id));
  if (!D.allowTechnical) {
    const simple = pool.filter((e) => !lib.TECHNICAL.has(e.id));
    if (simple.length >= 4) pool = simple;
  }

  /* Movements the person asked for by name. Generics ("squats") resolve to
     the first variant they can do; specific names are honoured even if the kit
     isn't in their saved list — saying "pull-ups" means you have a bar. */
  const include = [];
  for (const want of intent.include) {
    const candidates = (want.ids || []).map((id) => EX_MAP[id]).filter(Boolean).filter(allowed);
    let hit = want.generic ? candidates.find(usable) || null : candidates[0] || null;
    if (!hit && want.generic && candidates.length) hit = candidates[candidates.length - 1];
    if (hit && !include.includes(hit)) {
      include.push(hit);
      if (!usable(hit)) notes.push(`Kept ${hit.name} in because you asked for it — I'm assuming you have the kit.`);
    } else if (!hit && (want.ids || []).length) {
      notes.push(`Left out ${want.label}: it clashes with something you asked me to avoid.`);
    }
  }

  let focus = focusMatcher(intent);
  // "Something sweaty" leans on conditioning moves without banning the rest,
  // which matters when the kit and the no-jumping rule leave few of them.
  if (intent.cardio && !focus) focus = [{ label: "Conditioning", test: isCardioish }, { label: null, test: () => true }];
  const penalise = ["hard", "brutal"].includes(level) ? null : lib.TECHNICAL;

  /* A refinement ("harder", "shorter", "swap the swings") keeps the same
     format and movements and only changes what was asked; ctx.keep carries
     the previous build. Anything newly excluded drops out, anything newly
     named goes in. */
  const keep = ctx.keep || null;
  const format = intent.format || (intent.goal && "Strength") || keep?.format || chooseFormat(intent, minutes, rng);
  const kept = (keep?.ids || []).map((id) => EX_MAP[id]).filter((e) => e && allowed(e) && !include.includes(e));
  const seedWith = (list, n, inPool) => {
    if (!keep) return list;
    const room = Math.max(0, Math.max(n, list.length) - list.length);
    return [...kept.filter((e) => inPool.includes(e) || usable(e)).slice(0, room), ...list];
  };
  const preferEquip = new Set(intent.equipment);

  /* Strength days: sets × reps on compound lifts, ordered big to small. */

  if (format === "Strength") {
    const goal = intent.goal || "strength";
    const liftable = pool.filter((e) => e.unit === "reps" && ["squat", "hinge", "press", "row", "pushup", "pullup", "lunge", "curl"].includes(e.pattern)
      && (!lib.TECHNICAL.has(e.id) || include.includes(e)) && !isCardioish(e));
    const loaded = liftable.filter((e) => e.prop || e.equip.some((q) => ["barbell", "dumbbells", "kettlebell", "cable_machine", "leg_press", "landmine"].includes(q) && equip.has(q)));
    const lp = loaded.length >= 2 ? loaded : liftable;
    const perLift = goal === "strength" ? 11 : 7;
    const n = Math.min(goal === "strength" ? 4 : 6, Math.max(1, Math.round(minutes / perLift)));
    const named = include.filter((e) => lp.includes(e) || e.unit === "reps");
    const lifts = selectMovements({ pool: lp, n: Math.max(n, named.length), include: seedWith(named, n, lp), focus, preferEquip, cardio: false, rng, technical: penalise });
    const order = { squat: 0, hinge: 0, press: 1, pushup: 1, pullup: 1, row: 1, lunge: 2, curl: 3 };
    lifts.sort((a, b) => (order[a.pattern] ?? 2) - (order[b.pattern] ?? 2));
    const bodyweight = lifts.every((e) => !isLoaded(e, equip));
    const scheme = goal === "muscle"
      ? { easy: [3, "12"], moderate: [3, "10"], hard: [4, "10"], brutal: [4, "12"] }[level]
      : bodyweight
        ? { easy: [3, "8"], moderate: [4, "10"], hard: [5, "12"], brutal: [5, "15"] }[level]
        : { easy: [3, "8"], moderate: [5, "5"], hard: [5, "3"], brutal: [6, "2"] }[level];
    const rest = goal === "muscle" ? "60–90s" : bodyweight ? "90s" : "2–3 min";
    if (bodyweight && goal === "strength") notes.push("No load to work with, so pick the hardest variation you can do for clean reps.");
    const wod = {
      title: titleFor(intent, goal === "muscle" ? "Muscle" : "Strength"),
      format: "Sets x Reps",
      meta: `${scheme[0]} sets of ${scheme[1]} per lift · rest ${rest}`,
      level, difficulty: D.name, target: minutes,
      movements: lifts.map((m) => ({
        name: m.name, reps: `${scheme[0]}×${scheme[1]}`, pattern: m.pattern, prop: m.prop, scale: m.scale,
        load: isLoaded(m, equip) ? (goal === "muscle" ? "a weight you can just finish the set with" : level === "easy" ? "moderate and clean" : "build to a heavy set") : undefined,
      })),
      showLoads: goal === "strength" && !bodyweight,
      intensity: 1, tempo: goal === "muscle" ? "none" : "none", rolled: [],
    };
    return finish(wod, intent, notes, source, equip, minutes, lifts.length < n, { format: "Strength", ids: lifts.map((e) => e.id) });
  }

  /* Conditioning formats. */
  const adj = D.moves;
  let n;
  if (format === "Tabata") n = Math.max(1, Math.min(6, Math.round((minutes + 1) / 5)));
  else if (format === "EMOM") n = minutes >= 20 ? 4 : minutes >= 12 ? 3 : 2;
  else if (format === "Chipper") n = Math.max(4, Math.min(10, Math.round(minutes / 4) + adj));
  else if (format === "RFT") n = Math.max(2, Math.min(5, 3 + Math.round(adj / 2)));
  else n = Math.max(2, Math.min(6, (minutes <= 8 ? 3 : 4) + Math.round(adj / 2)));
  n = Math.max(n, Math.min(include.length, 8));
  // Refining keeps the movement count; only Tabata's count follows the clock.
  if (keep && format !== "Tabata") n = Math.max(2, kept.length + include.length);

  if (format === "EMOM" || format === "Tabata") {
    // A 60-second minute can't hold a 400m run or a long hold sensibly.
    pool = pool.filter((e) => e.unit !== "min" || include.includes(e));
  }
  if (format === "Tabata") {
    // Twenty seconds flat out suits burpees and bikes, not a max-effort bench press.
    const fit = pool.filter((e) => isCardioish(e) || (e.equip.includes("bodyweight") && ["squat", "lunge", "core", "pushup"].includes(e.pattern) && e.unit === "reps" && !lib.TECHNICAL.has(e.id)));
    if (fit.length >= 2) pool = fit;
  }

  const moves = selectMovements({ pool, n, include: seedWith(include, n, pool), focus, preferEquip, cardio: intent.cardio, rng, technical: penalise });
  const thin = moves.length < n;
  if (!moves.length) {
    return { kind: "empty", say: "Everything I could use clashes with what you asked me to avoid. Try loosening one of them — or ask for a stretch instead." };
  }

  const base = {
    AMRAP: { reps: 10, cal: 12, m: 200, carry: 20, sec: 30 },
    EMOM: { reps: 10, cal: 12, m: 150, carry: 20, sec: 35 },
    RFT: { reps: 12, cal: 15, m: 200, carry: 30, sec: 40 },
    Tabata: { reps: 0, cal: 0, m: 0, carry: 0, sec: 20 },
    Chipper: { reps: 20, cal: 20, m: 400, carry: 60, sec: 60 },
  }[format];

  let wod;
  if (format === "Tabata") {
    const blocks = moves.length;
    wod = {
      format: "Tabata",
      meta: `20s work / 10s rest × 8 per movement · ${blocks} block${blocks > 1 ? "s" : ""}, ~1 min easy between`,
      movements: moves.map((m) => ({ name: m.name, reps: "8 × 20s", pattern: m.pattern, prop: m.prop, scale: m.scale })),
      timerCfg: { work: 20, rest: 10, rounds: 8 * blocks },
      est: blocks * 4 + (blocks - 1),
    };
  } else if (format === "EMOM") {
    const list = moves.map((m) => {
      const p = prescribe(m, base, level, lib);
      // Keep each minute's work under ~45s so there's actual rest.
      let reps = p.reps;
      const s = seconds(m, reps);
      if (s > 45 && m.unit === "reps") reps = `${tidy(45 / secPerUnit(m))}`;
      if (s > 45 && m.unit === "cal") reps = `${tidy(45 / 4)} cal`;
      if (s > 45 && m.unit === "sec") reps = "40s";
      return { name: m.name, reps, pattern: m.pattern, prop: m.prop, scale: m.scale };
    });
    wod = {
      format: "EMOM",
      meta: `Every minute on the minute for ${minutes} minutes, rotating ${list.length > 1 ? `through ${list.length} movements` : "one movement"}`,
      movements: list,
      timerCfg: { total: minutes * 60 },
      est: minutes,
    };
  } else if (format === "Chipper") {
    // One long list, sized to the time — but nobody should be handed 100
    // muscle-ups. Amounts are capped per movement type, and if one pass can't
    // fill a long session the list is repeated rather than inflated.
    const cap = (m) => (lib.TECHNICAL.has(m.id) ? 25 : 50);
    const capAmount = (m) => ({ cal: 50, sec: 90, min: 8, m: m.pattern === "carry" ? 200 : 1000 }[m.unit] ?? cap(m));
    const capSec = (m) => (m.unit === "sec" ? 90 : capAmount(m) * secPerUnit(m));
    // Water-fill: movements that hit their cap keep it, and the time they
    // can't use is shared among the rest.
    const allocate = (total) => {
      const order = [...moves].sort((a, b) => capSec(a) - capSec(b));
      const out = new Map();
      let left = total;
      order.forEach((m, i) => {
        const share = left / (order.length - i);
        const t = Math.min(capSec(m), share);
        out.set(m, t);
        left -= t;
      });
      return { out, left };
    };
    let rounds = 1;
    let alloc = allocate((minutes * 60) - 15 * moves.length);
    while (alloc.left > 60 && rounds < 3) {
      rounds++;
      alloc = allocate((minutes * 60) / rounds - 15 * moves.length);
    }
    // The time is the target for someone at this level, so a harder session
    // packs more work into it rather than adding minutes.
    const levelFactor = Math.sqrt(D.vol);
    const list = moves.map((m) => {
      const amount = (alloc.out.get(m) / secPerUnit(m)) * (m.unit === "sec" || m.unit === "min" ? 1 : levelFactor);
      let reps;
      if (m.unit === "cal") reps = `${tidy(Math.min(50, amount))} cal`;
      else if (m.unit === "m") reps = `${Math.min(capAmount(m), Math.max(m.pattern === "carry" ? 20 : 100, Math.round(amount / (m.pattern === "carry" ? 10 : 50)) * (m.pattern === "carry" ? 10 : 50)))}m`;
      else if (m.unit === "sec") reps = `${Math.min(90, Math.max(30, Math.round(amount / 15) * 15))}s`;
      else if (m.unit === "min") reps = `${Math.min(8, Math.max(1, Math.round(amount)))} min`;
      else reps = `${tidy(Math.min(cap(m), amount))}`;
      return { name: m.name, reps, pattern: m.pattern, prop: m.prop, scale: m.scale, ex: m };
    });
    const once = moves.reduce((sum, m) => sum + alloc.out.get(m) + 15, 0) / 60;
    wod = {
      format: "Chipper",
      meta: rounds > 1 ? `${rounds} rounds, for time — work top to bottom each round` : "One round, for time — work top to bottom",
      movements: list.map(({ ex: _e, ...rest }) => rest),
      est: Math.round(once * rounds),
    };
  } else if (format === "RFT") {
    // Long sessions get bigger rounds rather than fifteen tiny ones.
    const sized = (k) => {
      const b = Object.fromEntries(Object.entries(base).map(([key, v]) => [key, v * k]));
      const l = moves.map((m) => ({ ...prescribe(m, b, level, lib), ex: m }));
      return { list: l, roundSec: l.reduce((s, x) => s + seconds(x.ex, x.reps) + 10, 0) };
    };
    let { list, roundSec } = sized(1);
    const want = (minutes * 60) / Math.max(30, roundSec);
    if (want > 8) ({ list, roundSec } = sized(want / 8));
    const rounds = Math.max(2, Math.min(10, Math.round((minutes * 60) / Math.max(30, roundSec))));
    wod = {
      format: "RFT",
      meta: `${rounds} rounds for time`,
      movements: list.map((x) => ({ name: x.ex.name, reps: x.reps, pattern: x.ex.pattern, prop: x.ex.prop, scale: x.ex.scale })),
      est: Math.round((rounds * roundSec) / 60),
    };
  } else {
    wod = {
      format: "AMRAP",
      meta: `${minutes} minute AMRAP`,
      movements: moves.map((m) => ({ ...prescribe(m, base, level, lib), name: m.name, pattern: m.pattern, prop: m.prop, scale: m.scale })),
      timerCfg: { total: minutes * 60 },
      est: minutes,
    };
  }
  for (const m of wod.movements) delete m.sec;

  wod = {
    title: titleFor(intent, FORMAT_LABEL[format]),
    level, difficulty: D.name, target: minutes, rolled: [], tempo: "none", showLoads: false, intensity: 1,
    ...wod,
  };
  if (format === "Tabata" && intent.minutes && Math.abs(wod.est - minutes) >= 3) {
    notes.push(`Tabata comes in 4-minute blocks, so this runs about ${wod.est} minutes.`);
  }
  return finish(wod, intent, notes, source, equip, minutes, thin, { format, ids: moves.map((e) => e.id) });
}

const LOAD_KIT = ["barbell", "dumbbells", "kettlebell", "cable_machine", "leg_press", "landmine", "sandbag"];
function isLoaded(ex, equip) {
  return ex.equip.some((q) => LOAD_KIT.includes(q) && equip.has(q)) && !(ex.equip.includes("bodyweight") && !ex.prop);
}

function titleFor(intent, formatLabel) {
  const labels = intent.focus.map((k) => FOCUS.find((f) => f.key === k)?.label).filter(Boolean);
  let lead = labels.length ? labels.slice(0, 2).join(" & ") : intent.cardio ? "Conditioning" : "";
  if (lead === "Full body" && labels.length === 1) lead = "Full-body";
  return lead ? `${lead} ${formatLabel}` : formatLabel === "Strength" || formatLabel === "Muscle" ? `${formatLabel} session` : formatLabel;
}

function finish(wod, intent, notes, source, equip, minutes, thin, keepInfo) {
  if (thin) {
    notes.push("Your kit and the things you're avoiding leave only a few movements, so this is shorter on variety than usual.");
  }
  for (const key of intent.avoid) notes.push(capitalise(`${BODY_PARTS[key].why}.`));
  if (intent.avoid.length) notes.push("If anything hurts, stop. Sore and stiff is fine to work around; sharp or getting worse needs a professional.");

  const heard = describeIntent(intent, { source, equip, minutes, level: wod.level, format: wod.format });
  wod.note = notes[0] && notes[0].length < 120 ? notes[0] : undefined;
  wod.fromTalk = true;
  wod.request = intent.texts.join(" → ");

  const recovery = intent.recovery
    ? { yoga: intent.recovery.yoga, breath: intent.recovery.breath }
    : intent.avoid.map((k) => BODY_PARTS[k].yoga).find(Boolean)
      ? { yoga: intent.avoid.map((k) => BODY_PARTS[k].yoga).find(Boolean), breath: null }
      : null;

  return { kind: "workout", wod, notes, heard, recovery, say: sayFor(wod, intent), keep: keepInfo };
}

function capitalise(s) {
  return s.charAt(0).toUpperCase() + s.slice(1);
}

const EQUIP_NAMES = {
  bodyweight: "bodyweight", dumbbells: "dumbbells", kettlebell: "kettlebell", barbell: "barbell", squat_rack: "rack",
  pull_up_bar: "pull-up bar", gymnastic_rings: "rings", box: "box", jump_rope: "jump rope", bands: "bands",
  rowing_machine: "rower", assault_bike: "bike", cardio_machine: "cardio machine", sandbag: "sandbag", sled: "sled",
  wall_ball: "med ball", bench: "bench", cable_machine: "cables", dip_bars: "dip bars", ab_wheel: "ab wheel",
  leg_press: "leg machine", pool: "pool", space_to_run: "space to run", battle_rope: "battle rope", rope_to_climb: "climbing rope",
};

/* The "here's what I heard" chips. Each says what was understood and whether
   it came from the request or was filled in. */
export function describeIntent(intent, built = {}) {
  const chips = [];
  chips.push({ kind: "time", text: `${built.minutes || intent.minutes || 20} min`, assumed: !intent.minutes });
  if (built.level) chips.push({ kind: "level", text: capitalise(built.level), assumed: !intent.level });
  if (built.format) chips.push({ kind: "format", text: built.format === "Sets x Reps" ? (intent.goal === "muscle" ? "Muscle building" : "Strength") : FORMAT_LABEL[built.format] || built.format, assumed: !intent.format && !intent.goal });
  for (const k of intent.focus) chips.push({ kind: "focus", text: FOCUS.find((f) => f.key === k)?.label });
  if (intent.cardio) chips.push({ kind: "focus", text: "Conditioning" });
  if (built.source === "bodyweight") chips.push({ kind: "equip", text: "No equipment" });
  else if (built.source === "named") chips.push({ kind: "equip", text: intent.equipment.map((e) => EQUIP_NAMES[e] || e).join(" + ") });
  else if (built.source === "gym") chips.push({ kind: "equip", text: "Full gym" });
  else if (built.source === "saved") chips.push({ kind: "equip", text: "Your saved kit", assumed: true });
  for (const i of intent.include) chips.push({ kind: "include", text: i.label });
  for (const e of intent.exclude) chips.push({ kind: "exclude", text: e.label });
  for (const id of intent.noEquipment) chips.push({ kind: "exclude", text: EQUIP_NAMES[id] || id });
  for (const k of intent.avoid) chips.push({ kind: "avoid", text: `Sparing ${BODY_PARTS[k].label}` });
  for (const k of intent.avoidFocus) chips.push({ kind: "exclude", text: FOCUS.find((f) => f.key === k)?.label });
  return chips.filter((c) => c.text);
}

function sayFor(wod, intent) {
  const lvl = wod.level === "moderate" ? "" : `${wod.level} `;
  const article = /^(easy|[0-9]*8|11|18)/.test(`${lvl}${wod.target}`) && (lvl === "easy " || /^(8|11|18)/.test(String(wod.target)) && !lvl) ? "an" : "a";
  const what = wod.format === "Sets x Reps" ? `${intent.goal === "muscle" ? "muscle-building" : "strength"} session` : wod.format === "RFT" ? "for-time workout" : `${wod.format === "AMRAP" ? "AMRAP" : wod.format}`;
  const foc = intent.focus.map((k) => FOCUS.find((f) => f.key === k)?.label.toLowerCase()).filter(Boolean);
  const focusText = foc.length ? ` for ${foc.join(" and ")}` : "";
  const est = wod.est && wod.format !== "AMRAP" && wod.format !== "EMOM" ? `, about ${wod.est} minutes` : "";
  const list = wod.movements.map((m) => m.name.replace(/\s*\(.*?\)/, "")).join(", ");
  return `Here's ${article} ${lvl}${wod.target}-minute ${what}${focusText}${est}: ${list}.`;
}

export const __test = { FOCUS, BODY_PARTS, EQUIP_WORDS, aliasIndex, markNegation };
