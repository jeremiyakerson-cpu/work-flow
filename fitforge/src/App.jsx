import React, { useState, useEffect, useRef, useMemo } from "react";
import {
  Dumbbell, Wind, Wrench, Settings2, Play, Pause, RotateCcw, X,
  ChevronRight, Check, Shuffle, Clock, Flame, ChevronLeft, Home as HomeIcon,
  Target, ArrowRight, HeartPulse, Volume2, Sunrise, Moon, Search, Star, Share2, CalendarDays, TrendingUp, Timer as TimerIcon, Plus, Sparkles, Pencil, Download, Upload,
  Mic, Send
} from "lucide-react";
import { parseRequest, mergeIntent, buildTalkWorkout } from "./talk.js";

/* ============================== THEME ==============================
   Daylight: a warm off-white ground, white cards floating on soft shadows,
   generous rounding. Reads as approachable rather than clinical — the point is
   that opening this app shouldn't feel like clocking in.

   Two things from the gym world still do real work:
   1. Bumper plate colours encode intensity — the scale a lifter already reads.
   2. Numbers are the product, so they're set in tabular figures at display size.
==================================================================== */
const T = {
  // Grounds
  ink: "#F6F5F2",        // page background
  inkDeep: "#EDEBE5",    // recessed areas
  surface: "#FFFFFF",    // cards
  surface2: "#F1EFE9",   // insets, tracks, chips
  surfaceLift: "#E8E5DD",
  line: "#E4E1DA",
  hairline: "rgba(0,0,0,0.05)",

  // Type
  chalk: "#16181D",      // primary text
  chalkDim: "#5A5F68",   // secondary text
  muted: "#7C818B",      // tertiary text

  // On a filled accent, type goes white — kept as its own token so buttons
  // don't accidentally inherit the (now pale) page colour.
  onAccent: "#FFFFFF",

  // Accents. Each has three roles, and they aren't interchangeable:
  //   <name>      fills a button and carries white text
  //   <name>Dim   a pastel wash for tinted tiles
  //   <name>Ink   a darker shade for text/icons sitting ON that pastel
  // Every pair below clears WCAG AA at 4.5:1 — a light ground is much less
  // forgiving than a dark one, and the dark-theme accents failed badly here.
  ember: "#DB360A",
  emberDim: "#FFE7DF",
  emberInk: "#C83109",
  sage: "#3F845B",
  sageDim: "#E4F4EA",
  sageInk: "#3A7A54",
  amber: "#A06805",
  amberDim: "#FDF0D6",
  amberInk: "#966104",
  violet: "#8B5CCA",
  violetDim: "#F2E8FF",
  violetInk: "#8251C6",
  cyan: "#297ABF",
  cyanDim: "#E2EFFC",
  cyanInk: "#266FAF",
  breath: "#327E94",
  breathDim: "#E0F0F5",
  breathInk: "#2E7489",
};

/* Competition bumper plate colours, heaviest to lightest. */
const PLATE = ["#D64545", "#3C7BD4", "#D9A520", "#4FA86A", "#8B8F98"];
function plateForIntensity(rpe) {
  if (!rpe) return T.muted;
  if (rpe >= 9) return PLATE[0];
  if (rpe >= 7) return PLATE[1];
  if (rpe >= 5) return PLATE[2];
  return PLATE[3];
}

const display = {
  fontFamily: '"Arial Narrow", "Helvetica Neue Condensed", Impact, sans-serif',
  textTransform: "uppercase",
  letterSpacing: "0.03em",
  fontWeight: 800,
};

/* Tabular figures so digits never shift width as a timer counts. */
const numeric = {
  fontFamily: '"SF Mono", "JetBrains Mono", "Roboto Mono", ui-monospace, monospace',
  fontVariantNumeric: "tabular-nums",
  fontFeatureSettings: '"tnum" 1',
  letterSpacing: "-0.02em",
};

const grainUrl =
  "url(\"data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='120' height='120'><filter id='n'><feTurbulence type='fractalNoise' baseFrequency='0.8' numOctaves='3'/></filter><rect width='120' height='120' filter='url(%23n)' opacity='0.025'/></svg>\")";

const chalkboardBg = {
  backgroundColor: T.ink,
};

/* Cards lift off the ground with a soft shadow rather than a border — the
   single biggest difference between a light UI that feels premium and one
   that feels like a form. */
function cardStyle(accent) {
  return {
    background: T.surface,
    border: accent ? `1px solid ${accent}33` : `1px solid ${T.hairline}`,
    boxShadow: "0 2px 12px rgba(22,24,29,0.05)",
    borderRadius: 18,
  };
}

/* The workout board keeps a dark surface even in daylight: it's the one place
   where a slate reads correctly, and it makes the hero object unmistakable. */
const slateStyle = {
  background: "linear-gradient(168deg, #23272E, #16181D)",
  border: "1px solid rgba(255,255,255,0.08)",
  boxShadow: "0 8px 28px rgba(22,24,29,0.22)",
};

function ChalkRule({ color = "#FFFFFF", className = "" }) {
  return (
    <svg viewBox="0 0 300 6" height="5" width="100%" className={className} preserveAspectRatio="none">
      <path d="M2,3.4 C60,1.8 110,4.2 165,2.6 C215,1.4 258,3.6 297,2.2"
        stroke={color} strokeWidth="2.2" fill="none" strokeLinecap="round" opacity="0.7" />
      <path d="M18,4.4 C70,3.2 120,4.8 172,3.6" stroke={color} strokeWidth="1" fill="none" strokeLinecap="round" opacity="0.28" />
    </svg>
  );
}

function emberButton(color = T.ember) {
  return {
    background: color,
    color: T.onAccent,
    boxShadow: `0 3px 12px ${color}3D`,
    ...display,
  };
}

/* Dark pill for the primary action — highest contrast available on a light
   ground, and it keeps accent colour meaningful elsewhere. */
function inkButton() {
  return {
    background: T.chalk,
    color: T.surface,
    boxShadow: "0 3px 14px rgba(22,24,29,0.22)",
    ...display,
  };
}

function SectionLabel({ children, color = T.muted }) {
  return (
    <p style={{ ...display, color, fontSize: 11, letterSpacing: "0.14em" }} className="mb-2.5">
      {children}
    </p>
  );
}

function Stat({ value, label, color = T.chalk, size = 26 }) {
  return (
    <div className="text-center">
      <p style={{ ...numeric, color, fontSize: size, fontWeight: 700, lineHeight: 1.1 }}>{value}</p>
      <p className="text-[10px] mt-0.5" style={{ color: T.muted, letterSpacing: "0.04em" }}>{label}</p>
    </div>
  );
}

/* ============================== EQUIPMENT ============================== */
const EQUIPMENT = [
  { id: "bodyweight", name: "Bodyweight only", icon: "🤸" },
  { id: "barbell", name: "Barbell & plates", icon: "🏋️" },
  { id: "squat_rack", name: "Squat rack", icon: "🔲" },
  { id: "bench", name: "Bench", icon: "🛏️", scale: { down: "Use dumbbells instead of a barbell for an easier setup and range.", up: "Add chains or bands for accommodating resistance." } },
  { id: "dumbbells", name: "Dumbbells", icon: "🏋️‍♀️" },
  { id: "kettlebell", name: "Kettlebell", icon: "🔔" },
  { id: "pull_up_bar", name: "Pull-up bar", icon: "🌲" },
  { id: "gymnastic_rings", name: "Gymnastic rings", icon: "⭕" },
  { id: "dip_bars", name: "Dip bars", icon: "⊔" },
  { id: "box", name: "Plyo box", icon: "◧" },
  { id: "jump_rope", name: "Jump rope", icon: "➰" },
  { id: "bands", name: "Bands", icon: "➿" },
  { id: "assault_bike", name: "Assault bike", icon: "🚲" },
  { id: "rowing_machine", name: "Rowing machine", icon: "🚣" },
  { id: "sandbag", name: "Sandbag", icon: "💼" },
  { id: "sled", name: "Sled", icon: "🛷" },
  { id: "battle_rope", name: "Battle rope", icon: "🪢" },
  { id: "wall_ball", name: "Wall / medicine ball", icon: "🏐" },
  { id: "space_to_run", name: "Space to run", icon: "🏃" },
  { id: "cable_machine", name: "Cable machine", icon: "🔗" },
  { id: "landmine", name: "Landmine attachment", icon: "📐" },
  { id: "leg_press", name: "Leg press / leg machine", icon: "🦵" },
  { id: "rope_to_climb", name: "Rope to climb", icon: "🪜" },
  { id: "tire", name: "Tire", icon: "🛞" },
  { id: "yoke", name: "Yoke", icon: "🎒" },
  { id: "atlas_stone", name: "Atlas stone / D-ball", icon: "🪨" },
  { id: "ab_wheel", name: "Ab wheel", icon: "☸️" },
  { id: "cardio_machine", name: "Cardio machine (treadmill/elliptical/stair)", icon: "🏃‍♂️" },
  { id: "pool", name: "Swimming pool", icon: "🏊" },
];
const EQUIP_MAP = Object.fromEntries(EQUIPMENT.map((e) => [e.id, e]));
/* ============================== MUSCLE HEAT MAP ==============================
   A body silhouette where trained areas glow by volume, in the plate colours
   already used for effort. Reads in a glance where a bar chart needs parsing.
==================================================================== */
const BODY_REGIONS = {
  Chest:      "M40,44 h20 a5,5 0 0 1 5,5 v12 a5,5 0 0 1 -5,5 h-20 a5,5 0 0 1 -5,-5 v-12 a5,5 0 0 1 5,-5 z",
  Shoulders:  "M28,42 a8,8 0 0 1 8,-6 v12 a8,8 0 0 1 -8,-6 z M72,42 a8,8 0 0 0 -8,-6 v12 a8,8 0 0 0 8,6 z",
  Arms:       "M24,52 l6,2 -3,26 -7,-2 z M76,52 l-6,2 3,26 7,-2 z",
  Back:       "M38,40 h24 v6 h-24 z",
  Core:       "M40,67 h20 v20 h-20 z",
  Glutes:     "M39,88 h22 v11 h-22 z",
  Quads:      "M40,100 h9 v26 h-9 z M51,100 h9 v26 h-9 z",
  Hamstrings: "M40,100 h9 v26 h-9 z M51,100 h9 v26 h-9 z",
  "Full Body": "",
};

function MuscleHeatMap({ counts, size = 190 }) {
  const values = Object.entries(counts).filter(([k]) => k !== "Full Body");
  const max = Math.max(1, ...values.map(([, v]) => v));

  // Under-trained areas should look cold, not just dim — so the scale runs from
  // the app's dead surface colour up through the plate ramp.
  const heatFor = (v) => {
    if (!v) return T.surface2;
    const r = v / max;
    if (r > 0.75) return PLATE[0];
    if (r > 0.5) return PLATE[2];
    if (r > 0.25) return PLATE[3];
    return "#3A4250";
  };

  return (
    <div className="flex items-center gap-4">
      <svg viewBox="0 0 100 150" width={size * 0.62} height={size} style={{ overflow: "visible" }}>
        {/* head + frame, always neutral */}
        <circle cx="50" cy="24" r="10" fill={T.surface2} />
        <path d="M43,127 h6 v14 h-6 z M51,127 h6 v14 h-6 z" fill={T.surface2} />
        {Object.entries(BODY_REGIONS).map(([name, d]) =>
          d ? (
            <path key={name} d={d} fill={heatFor(counts[name] || 0)}
              stroke={T.ink} strokeWidth="0.8"
              style={{ transition: "fill 500ms ease" }} />
          ) : null
        )}
      </svg>

      <div className="flex-1 space-y-1">
        {values.sort((a, b) => b[1] - a[1]).slice(0, 6).map(([name, v]) => (
          <div key={name} className="flex items-center gap-2">
            <span className="w-2.5 h-2.5 rounded-sm shrink-0" style={{ background: heatFor(v) }} />
            <span className="flex-1 text-[11px]" style={{ color: v ? T.chalk : T.muted }}>{name}</span>
            <span style={{ ...numeric, fontSize: 11, color: T.muted }}>{v}</span>
          </div>
        ))}
        {values.filter(([, v]) => !v).length > 0 && (
          <p className="text-[10px] pt-1" style={{ color: T.muted }}>
            Cold: {values.filter(([, v]) => !v).map(([n]) => n).join(", ")}
          </p>
        )}
      </div>
    </div>
  );
}

/* ============================== ICON BADGES ==============================
   Custom line-art glyphs, one per movement pattern. Drawn as SVG paths on a
   shared 24x24 grid so every icon shares the same stroke weight and optical
   size, which system emoji can't guarantee across devices.
==================================================================== */
const PATTERN_ICON = {
  squat: "M6 4h12 M12 4v6 M12 10l-4 5v5 M12 10l4 5v5",
  hinge: "M5 20V9 M5 9c4-4 10-4 14 0 M19 9v4 M8 20h8",
  lunge: "M7 4v6 M7 10l-3 6 M7 10l6 3 4 7 M17 13h4",
  press: "M4 4h16 M12 4v7 M9 11h6 M12 11v9 M8 20h8",
  pushup: "M3 17h18 M6 17v-4 M6 13h9 M15 13l3-3 M18 10a1.6 1.6 0 100-.1",
  pullup: "M3 5h18 M9 5v5 M15 5v5 M12 10v6 M9 20h6",
  row: "M4 18c4-3 12-3 16 0 M8 14l4-6 M12 8l6 2 M12 8V4",
  jump: "M12 21V7 M7 12l5-5 5 5 M5 21h14",
  run: "M14 4a1.6 1.6 0 100-.1 M13 8l-4 4 3 3-1 5 M12 12l5 2 M9 12l-4 2",
  carry: "M12 3v14 M8 7h8 M6 9h2 M16 9h2 M8 21h8",
  swing: "M12 3a3 3 0 016 0 M9 7h6l2 8a5 5 0 01-10 0z",
  curl: "M8 20v-7a4 4 0 018 0v3 M6 8h4 M14 4h4",
  calf: "M9 3v9l-3 5 M9 12l4 3 M6 20h10 M16 15v5",
  core: "M4 12a8 5 0 1016 0a8 5 0 10-16 0 M12 7v10",
  plank: "M3 15h18 M6 15v-3 M6 12h12 M18 12a1.6 1.6 0 100-.1",
  yoga: "M12 4a1.8 1.8 0 100-.1 M12 8v6 M7 10l5 2 5-2 M8 20l4-6 4 6",
};

/* Movements whose equipment can carry the motion. Everything else keeps the
   static glyph — a loop with nothing real to move would just be noise. */
const MOVING_ICON = {
  squat:  { legs: "M7 21v-8 M17 21v-8", rig: "bar", barY: 9 },
  press:  { legs: "M12 21v-9", rig: "bar", barY: 7 },
  hinge:  { legs: "M6 20V11 M18 20V11", rig: "bar", barY: 15 },
  row:    { legs: "M5 20V10 M12 10c4-3 7-1 7 2", rig: "bar", barY: 16 },
  curl:   { legs: "M8 21v-6 M16 21v-6", rig: "bell", pivot: "12px 5px" },
  swing:  { legs: "M12 3v5", rig: "bell", pivot: "12px 4px" },
  carry:  { legs: "M12 4v13 M8 21h8", rig: "bells" },
  jump:   { legs: "M4 6v4 M20 6v4", rig: "rope" },
  pullup: { legs: "M3 5h18", rig: "hang" },
};

const ICON_CSS = `
@keyframes ffBar   {0%,100%{transform:translateY(3px)}50%{transform:translateY(-4px)}}
@keyframes ffBell  {0%,100%{transform:rotate(-30deg)}50%{transform:rotate(30deg)}}
@keyframes ffBells {0%,100%{transform:translateY(0)}50%{transform:translateY(2px)}}
@keyframes ffRope  {0%,100%{transform:scaleY(1)}50%{transform:scaleY(.35)}}
@keyframes ffHang  {0%,100%{transform:translateY(2px)}50%{transform:translateY(-3px)}}
@keyframes ffTempo {
  0%   {width:0%;   background:var(--ecc)}
  55%  {width:100%; background:var(--ecc)}
  70%  {width:100%; background:var(--pause)}
  100% {width:0%;   background:var(--con)}
}
@keyframes ffRing  {to{stroke-dashoffset:var(--off)}}
.ff-bar   {animation:ffBar 2.1s ease-in-out infinite}
.ff-bell  {animation:ffBell 1.8s ease-in-out infinite}
.ff-bells {animation:ffBells 1.3s ease-in-out infinite}
.ff-rope  {animation:ffRope 1s ease-in-out infinite; transform-origin:center}
.ff-hang  {animation:ffHang 1.9s ease-in-out infinite}
@media (prefers-reduced-motion: reduce){
  .ff-bar,.ff-bell,.ff-bells,.ff-rope,.ff-hang{animation:none}
}
`;

function IconBadge({ pattern, pose, color = T.ember, bg, size = 60, still = false }) {
  const inner = Math.round(size * 0.56);
  const move = !pose && !still ? MOVING_ICON[pattern] : null;
  const wrap = (children) => (
    <div style={{
      width: size, height: size, minWidth: size, borderRadius: "9999px",
      background: bg || "transparent",
      border: bg ? "none" : `2px solid ${color}55`,
      display: "flex", alignItems: "center", justifyContent: "center",
    }}>
      <svg width={inner} height={inner} viewBox="0 0 24 24" fill="none"
        stroke={color} strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round"
        style={{ overflow: "visible" }}>
        {children}
      </svg>
    </div>
  );

  if (!move) {
    const d = pose ? PATTERN_ICON.yoga : PATTERN_ICON[pattern] || PATTERN_ICON.squat;
    return wrap(<path d={d} />);
  }

  // The body stays put; only the load travels — which is what you actually
  // watch in a gym, and it sidesteps animating joints entirely.
  const rig = {
    bar: (
      <g className="ff-bar">
        <path d={`M4 ${move.barY}h16`} />
        <rect x="2" y={move.barY - 3} width="3" height="6" rx="1" fill={color} stroke="none" />
        <rect x="19" y={move.barY - 3} width="3" height="6" rx="1" fill={color} stroke="none" />
      </g>
    ),
    bell: (
      <g className="ff-bell" style={{ transformOrigin: move.pivot }}>
        <path d="M9 8h6l2 7a5 5 0 01-10 0z" />
      </g>
    ),
    bells: (
      <g className="ff-bells">
        <rect x="4" y="9" width="4" height="7" rx="1.5" fill={color} stroke="none" />
        <rect x="16" y="9" width="4" height="7" rx="1.5" fill={color} stroke="none" />
      </g>
    ),
    rope: (
      <g className="ff-rope"><ellipse cx="12" cy="13" rx="8" ry="7" /></g>
    ),
    hang: (
      <g className="ff-hang"><path d="M9 5v5 M15 5v5 M12 10v7 M9 21h6" /></g>
    ),
  }[move.rig];

  return wrap(<>
    <path d={move.legs} opacity="0.45" />
    {rig}
  </>);
}

/* ============================== DAILY DISCOVERY ==============================
   Three things to try today: a movement, a yoga flow, a breathing pattern.

   Two rules make this feel considered rather than random. It's seeded on the
   date, so the same day always shows the same picks — refreshing the app can't
   reroll them, and the list feels chosen rather than shuffled. And anything
   already logged or already saved is skipped, so it stays a suggestion of
   something *new*.
==================================================================== */
function daySeed(offset = 0) {
  const d = new Date();
  return Number(`${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, "0")}${String(d.getDate()).padStart(2, "0")}`) + offset * 7919;
}

/* Deterministic pick — same seed, same item, no matter how often it renders. */
function seededPick(list, seed) {
  if (!list.length) return null;
  return list[seed % list.length];
}

function dailyPicks(store) {
  const { history, favorites, equipment } = store;
  const doneNames = new Set(history.map((h) => h.name));

  const owned = (e) => e.equip.some((q) => q === "bodyweight" || equipment[q]);
  const freshMoves = EX.filter((e) => owned(e) && !doneNames.has(e.name) && !favorites.includes("ex:" + e.id));
  const move = seededPick(freshMoves.length ? freshMoves : EX.filter(owned), daySeed(0));

  const areas = Object.keys(YOGA_AREAS);
  const freshAreas = areas.filter((a) => !favorites.includes("yoga:" + a));
  const area = seededPick(freshAreas.length ? freshAreas : areas, daySeed(1));

  // Skip the protocols that need an acknowledgement — a daily suggestion
  // shouldn't be one that opens a risk warning.
  const gentle = BREATH_PATTERNS.filter((b) => !b.requiresAck && b.phases);
  const breath = seededPick(gentle, daySeed(2));

  return { move, area, breath };
}

/* ============================== PLAIN ENGLISH ==============================
   The app was written by someone who already knows CrossFit. A first-timer
   meets "AMRAP", "EMOM", "RFT" and "Chipper" with no idea what any of it means,
   so plain wording leads and the jargon becomes the subtitle — not the reverse.
==================================================================== */
const GLOSSARY = {
  AMRAP: { plain: "As many rounds as possible", full: "Set a clock and repeat the movements until time runs out. Count your rounds — that's your score." },
  EMOM: { plain: "Every minute, on the minute", full: "At the top of each minute you start the next set. Whatever's left of that minute is your rest, so working faster earns you more of it." },
  RFT: { plain: "Rounds for time", full: "Finish all the rounds as fast as you can. Your time is your score." },
  "For Time": { plain: "Finish as fast as you can", full: "Complete everything listed as quickly as you can manage. Your time is your score." },
  Tabata: { plain: "20 seconds hard, 10 seconds rest", full: "Eight rounds of 20 seconds of work and 10 seconds of rest — four minutes per movement. Go hard, the rest is short." },
  Chipper: { plain: "One long list, top to bottom", full: "A single round of everything on the list, worked through in order. No repeats — you just chip away at it." },
  Strength: { plain: "Heavy sets, long rests", full: "A few hard sets of one lift with proper rest between them. The goal is load, not breathlessness." },
  WOD: { plain: "Workout of the day", full: "Common shorthand in CrossFit gyms for the day's session." },
  RPE: { plain: "How hard it felt, 1 to 10", full: "Rate of perceived exertion. 1 is easy, 10 is everything you had. It's the simplest way to track effort without any equipment." },
  "1RM": { plain: "Your heaviest single rep", full: "One-rep max: the most weight you can lift once with decent form. Training weights are usually set as a percentage of it." },
  Rx: { plain: "As prescribed", full: "Doing the workout exactly as written, without scaling the weight or movements." },
  Hinge: { plain: "Bending at the hips", full: "Movements like deadlifts and kettlebell swings, where the hips travel back and the back stays flat." },
  Eccentric: { plain: "The lowering part of a rep", full: "The phase where the muscle lengthens under load — lowering the bar. Slowing it down makes a lift much harder." },
  Concentric: { plain: "The lifting part of a rep", full: "The phase where the muscle shortens — driving the weight back up." },
  Kip: { plain: "Using a swing for momentum", full: "A hip-driven swing that helps you get above the bar. Faster than strict, but harder on the shoulders." },
  Deficit: { plain: "Extra range of motion", full: "Standing on a plate or lowering past the usual floor, so the movement travels further and gets harder." },
};

/* Plain wording first, the shorthand underneath. */
function plainName(term) {
  return GLOSSARY[term]?.plain || term;
}

/* A term you can tap for an explanation, used the first time jargon appears
   rather than buried in a glossary nobody opens. */
function Term({ term, onExplain, color = T.muted, size = 11 }) {
  const g = GLOSSARY[term];
  if (!g) return <span style={{ color, fontSize: size }}>{term}</span>;
  return (
    <button
      onClick={(e) => { e.stopPropagation(); onExplain(term); }}
      className="inline-flex items-center gap-1"
      aria-label={`What does ${term} mean?`}
    >
      <span style={{ color, fontSize: size }}>{term}</span>
      <span className="inline-flex items-center justify-center rounded-full"
        style={{ width: size + 3, height: size + 3, border: `1px solid ${color}`, color, fontSize: size - 3, lineHeight: 1 }}>?</span>
    </button>
  );
}

function GlossarySheet({ term, onClose }) {
  const g = GLOSSARY[term];
  if (!term || !g) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-end" style={{ background: "rgba(22,24,29,0.42)", backdropFilter: "blur(2px)" }}>
      <div className="w-full rounded-t-[24px] p-5" style={{ background: T.surface }}>
        <div className="flex items-start justify-between mb-1">
          <div>
            <p style={{ ...display, color: T.ember, fontSize: 12, letterSpacing: "0.14em" }}>{term}</p>
            <h2 style={{ color: T.chalk, fontSize: 21, fontWeight: 800, lineHeight: 1.2 }}>{g.plain}</h2>
          </div>
          <button onClick={onClose} aria-label="Close"><X color={T.muted} /></button>
        </div>
        <p className="text-sm mt-3" style={{ color: T.chalkDim, lineHeight: 1.55 }}>{g.full}</p>
        <button onClick={onClose} className="w-full mt-5 py-3 rounded-full" style={{ ...inkButton(), fontSize: 15 }}>Got it</button>
      </div>
    </div>
  );
}

/* ============================== DIFFICULTY ==============================
   A real dial, not a label. Each level changes three things: how much work is
   prescribed, how long the piece runs, and which movements are eligible —
   because "harder" mostly means more volume, but it also means a beginner
   shouldn't be handed muscle-ups and snatches.
==================================================================== */
const TECHNICAL = new Set([
  "snatch", "cleanjerk", "muscleup", "barmuscleup", "hspu", "ringdip",
  "toestobar", "pistolsquat", "snatchbalance", "overheadsquat", "turkishgetup",
  "doubleunder", "ropeclimb", "wallwalk", "atlasstone", "pushjerk", "devilpress",
]);

const DIFFICULTY = {
  easy:     { id: "easy",     name: "Easy",     blurb: "Lower volume, simple movements", vol: 0.65, time: 0.7, moves: -1, allowTechnical: false, color: PLATE[3] },
  moderate: { id: "moderate", name: "Moderate", blurb: "A solid working session",        vol: 1.0,  time: 1.0, moves: 0,  allowTechnical: true,  color: PLATE[2] },
  hard:     { id: "hard",     name: "Hard",     blurb: "High volume, longer time domain", vol: 1.35, time: 1.3, moves: 1,  allowTechnical: true,  color: PLATE[1] },
  brutal:   { id: "brutal",   name: "Brutal",   blurb: "Redline. Scale it if you need to", vol: 1.7, time: 1.55, moves: 2, allowTechnical: true, color: PLATE[0] },
};
const DIFFICULTY_ORDER = ["easy", "moderate", "hard", "brutal"];

/* A little variation around the base so two workouts at the same difficulty
   aren't carbon copies. Kept small — it should feel like a different session,
   not a different difficulty. */
function jitter(base) {
  return base * (0.85 + Math.random() * 0.3);
}

/* Scale a rep count and keep it a tidy number — nobody programs 13 wall balls.
   Rounds to 5s only above 20 reps; below that, 5s are too coarse a step and
   adjacent difficulty levels collapse onto the same number. */
function scaleReps(base, level) {
  const raw = base * DIFFICULTY[level].vol;
  if (raw >= 20) return Math.round(raw / 5) * 5;
  if (raw >= 6) return Math.round(raw / 2) * 2;
  return Math.max(1, Math.round(raw));
}
function scaleTime(mins, level) {
  return Math.max(4, Math.round(mins * DIFFICULTY[level].time));
}
function scaleDistance(m, level) {
  return Math.max(100, Math.round((m * DIFFICULTY[level].vol) / 50) * 50);
}

/* ============================== MOVEMENT PATTERNS ==============================
   Human-readable names for the pattern each movement trains, so the detail page
   can show what it actually is rather than an internal key.
==================================================================== */
const PATTERN_LABEL = {
  squat: "Squat", hinge: "Hinge", lunge: "Lunge", press: "Vertical push",
  pushup: "Horizontal push", pullup: "Vertical pull", row: "Horizontal pull",
  jump: "Jump / plyometric", run: "Monostructural", carry: "Loaded carry",
  swing: "Ballistic hinge", curl: "Isolation", calf: "Isolation",
  core: "Core", plank: "Core / isometric",
};

/* Movements that ask a lot technically get a warning on their page, the same
   way a coach would flag them before you load a bar. */
function isComplex(ex) {
  return TECHNICAL.has(ex.id);
}

/* Alternatives, derived rather than hand-listed: same movement pattern, and
   for scaling down, something that needs less equipment or less skill.
   135 movements would be unmaintainable as hand-written swap lists. */
function alternativesFor(ex, equipment, direction = "down") {
  const owned = (e) => e.equip.some((q) => q === "bodyweight" || equipment[q]);

  // A substitute has to train the same thing. A push is never a scale-down for
  // a pull, however similar the muscles look on paper — so same-pattern always
  // outranks a shared-muscle match rather than being pooled with it.
  const samePattern = EX.filter((e) => e.id !== ex.id && e.pattern === ex.pattern && owned(e));
  const sameMuscle = EX.filter(
    (e) => e.id !== ex.id && e.pattern !== ex.pattern && owned(e) &&
      e.muscle.some((m) => ex.muscle.includes(m))
  );

  // Rough difficulty: technical skill, plus whether it's loaded.
  const hardness = (e) =>
    (TECHNICAL.has(e.id) ? 3 : 0) +
    (e.equip.includes("barbell") ? 2 : 0) +
    (e.equip.includes("bodyweight") ? 0 : 1);

  const base = hardness(ex);
  const pool = samePattern.length >= 2 ? samePattern : [...samePattern, ...sameMuscle];

  const candidates = pool.filter((e) =>
    direction === "down" ? hardness(e) <= base : hardness(e) >= base
  );

  // If nothing is strictly easier (or harder), fall back to the same pattern
  // rather than showing something unrelated.
  const finalPool = candidates.length ? candidates : samePattern;

  return finalPool
    .sort((a, b) => {
      // Prefer same-pattern, then closest in difficulty to what's wanted.
      const aSame = a.pattern === ex.pattern ? 0 : 1;
      const bSame = b.pattern === ex.pattern ? 0 : 1;
      if (aSame !== bSame) return aSame - bSame;
      return direction === "down" ? hardness(a) - hardness(b) : hardness(b) - hardness(a);
    })
    .slice(0, 4);
}

/* A demo link rather than an embedded clip: searching by name always resolves
   to current results, where a hardcoded video id eventually rots or 404s. */
function demoSearchUrl(name) {
  return "https://www.youtube.com/results?search_query=" + encodeURIComponent(name + " exercise demo form");
}

/* ============================== WARM-UP GENERATOR ==============================
   A warm-up is a short easy-pace AMRAP, generated the same way a WOD is, so it
   varies session to session instead of being the same seven stretches forever.
==================================================================== */
const WARMUP_POOL = [
  { name: "Mountain climbers", reps: 20, each: true, pattern: "core" },
  { name: "Penguin jumps", reps: 20, pattern: "jump" },
  { name: "Inch worm with push-up", reps: 12, pattern: "pushup" },
  { name: "Bodyweight good mornings", reps: 12, pattern: "hinge" },
  { name: "Bicycle crunches", reps: 12, pattern: "core" },
  { name: "Plank to supermans", reps: 8, each: true, pattern: "plank" },
  { name: "V-ups", reps: 8, pattern: "core" },
  { name: "Air squats", reps: 15, pattern: "squat" },
  { name: "Walking lunges", reps: 10, each: true, pattern: "lunge" },
  { name: "Arm circles", reps: 15, each: true, pattern: "curl" },
  { name: "Leg swings", reps: 12, each: true, pattern: "lunge" },
  { name: "Scapular pull-ups", reps: 8, pattern: "pullup" },
  { name: "Glute bridges", reps: 15, pattern: "hinge" },
  { name: "Cossack squats", reps: 8, each: true, pattern: "squat" },
  { name: "High knees", reps: 20, each: true, pattern: "run" },
  { name: "Butt kicks", reps: 20, each: true, pattern: "run" },
  { name: "Push-ups", reps: 10, pattern: "pushup" },
  { name: "Ring rows", reps: 10, pattern: "row" },
  { name: "Hollow rocks", reps: 15, pattern: "core" },
  { name: "World's greatest stretch", reps: 5, each: true, pattern: "lunge" },
];

function generateWarmup(prev) {
  const minutes = 7 + Math.floor(Math.random() * 4);        // 7-10 min
  const picked = [];
  const pool = [...WARMUP_POOL];
  // Spread across patterns so a warm-up isn't four core movements in a row.
  const wanted = ["core", "jump", "pushup", "hinge", "squat", "lunge", "run", "row", "pullup", "plank"];
  const shuffled = wanted.sort(() => Math.random() - 0.5);
  shuffled.forEach((pat) => {
    if (picked.length >= 4) return;
    const opts = pool.filter((m) => m.pattern === pat && !picked.includes(m));
    if (opts.length) picked.push(opts[Math.floor(Math.random() * opts.length)]);
  });
  while (picked.length < 4) {
    const m = pool[Math.floor(Math.random() * pool.length)];
    if (!picked.includes(m)) picked.push(m);
  }
  const wu = { minutes, movements: picked };
  // Don't hand back the same warm-up twice running.
  if (prev && prev.movements.map((m) => m.name).join() === picked.map((m) => m.name).join()) {
    return generateWarmup(null);
  }
  return wu;
}

/* ============================== WORKOUT TEMPO ==============================
   Tempo is a property of how you're training today, not of the movement — the
   same back squat is a different session at 3-1-1 than at 1-0-X. So it's chosen
   per workout and applies to every lift in it.

   Notation is the standard four figures: lowering, pause at the bottom, lifting,
   pause at the top. "X" means move the weight as fast as you can.
==================================================================== */
const TEMPO_SCHEMES = [
  { id: "none",     name: "No tempo",     notation: "—",       secs: null, blurb: "Move at whatever pace feels right." },
  { id: "control",  name: "Controlled",   notation: "2-0-1-0", secs: [2, 0, 1, 0], blurb: "A steady default. Keeps you honest without slowing the session down." },
  { id: "eccentric",name: "Slow eccentric",notation: "4-0-1-0", secs: [4, 0, 1, 0], blurb: "Four seconds down. Most growth stimulus per rep, and brutal at light loads." },
  { id: "paused",   name: "Paused",       notation: "3-2-1-0", secs: [3, 2, 1, 0], blurb: "Dead stop at the bottom kills the bounce and exposes weak positions." },
  { id: "explosive",name: "Explosive",    notation: "2-0-X-0", secs: [2, 0, 1, 0], blurb: "Lower under control, then drive as fast as you can. For power, not fatigue." },
  { id: "grind",    name: "Time under tension", notation: "3-1-3-1", secs: [3, 1, 3, 1], blurb: "Slow both directions. Use much lighter weight than you think." },
];

function tempoById(id) {
  return TEMPO_SCHEMES.find((t) => t.id === id) || TEMPO_SCHEMES[0];
}

/* Animated read-out of a tempo, so you can follow one rep with your eyes. */
function TempoBar({ scheme, color = T.ember, compact = false }) {
  const t = tempoById(scheme);
  if (!t.secs) return null;
  const [down, hold, up, top] = t.secs;
  const total = down + hold + up + top;
  const explosive = t.id === "explosive";

  return (
    <div className="rounded-xl p-3" style={compact ? { background: T.surface2 } : cardStyle()}>
      <div className="flex items-center justify-between mb-2">
        <span style={{ ...display, color: T.chalk, fontSize: 12 }}>{t.name}</span>
        <span style={{ ...numeric, color, fontSize: 12, fontWeight: 600 }}>{t.notation}</span>
      </div>
      <div className="h-2 rounded-full overflow-hidden" style={{ background: T.inkDeep }}>
        <div style={{
          height: "100%", borderRadius: 99, width: 0,
          "--ecc": PLATE[1], "--pause": PLATE[2], "--con": color,
          animation: `ffTempo ${total}s linear infinite`,
        }} />
      </div>
      {!compact && (
        <>
          <div className="flex justify-between mt-2" style={{ ...numeric, fontSize: 10, color: T.muted }}>
            <span style={{ color: PLATE[1] }}>{down}s down</span>
            {hold > 0 && <span style={{ color: PLATE[2] }}>{hold}s hold</span>}
            <span style={{ color }}>{explosive ? "explode up" : `${up}s up`}</span>
          </div>
          <p className="text-[11px] mt-2" style={{ color: T.muted }}>{t.blurb}</p>
        </>
      )}
    </div>
  );
}

/* ============================== LOAD PRESCRIPTION ==============================
   Turns a logged or estimated 1RM into an actual number to put on the bar.
   Percentages follow standard strength prescriptions by rep range.
==================================================================== */
const REP_PCT = [
  { reps: 1, pct: 1.0 }, { reps: 2, pct: 0.95 }, { reps: 3, pct: 0.93 },
  { reps: 5, pct: 0.87 }, { reps: 8, pct: 0.80 }, { reps: 10, pct: 0.75 },
  { reps: 12, pct: 0.70 }, { reps: 15, pct: 0.65 }, { reps: 20, pct: 0.60 },
];
function pctForReps(reps) {
  const n = parseInt(String(reps).match(/\d+/)?.[0] || "", 10);
  if (!n) return null;
  // Snap to the nearest standard rep bracket rather than interpolating noise.
  return REP_PCT.reduce((best, r) => (Math.abs(r.reps - n) < Math.abs(best.reps - n) ? r : best)).pct;
}

/* Round to something you can actually load: 2.5kg or 5lb increments. */
function roundToPlate(kg, units) {
  if (units === "lb") {
    const lb = kg * KG_TO_LB;
    return Math.round(lb / 5) * 5 / KG_TO_LB;
  }
  return Math.round(kg / 2.5) * 2.5;
}

/* Best available 1RM for a lift: a logged PR beats an estimate from history. */
function oneRmFor(name, prs, history) {
  const pr = prs[name];
  if (pr && pr.unit === "load") return { kg: pr.value, source: "your PR" };
  const logged = history.find((h) => h.name === name && h.loadKg);
  if (logged) return { kg: logged.loadKg * 1.15, source: "estimated from your log" };
  return null;
}

function suggestLoad(movementName, reps, intensityPct, store) {
  const rm = oneRmFor(movementName, store.prs, store.history);
  if (!rm) return null;
  const base = pctForReps(reps);
  if (!base) return null;
  const target = rm.kg * base * intensityPct;
  return {
    text: displayWeight(roundToPlate(target, store.settings.units), store.settings.units),
    pct: Math.round(base * intensityPct * 100),
    source: rm.source,
  };
}

/* Option 6 — load ring. Effort read as a filled ring in plate colours. */
function LoadRing({ rpe, size = 44, showNumber = true }) {
  const color = plateForIntensity(rpe);
  const r = size / 2 - 3;
  const circ = 2 * Math.PI * r;
  const pct = rpe ? rpe / 10 : 0;
  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} style={{ overflow: "visible" }}>
      <circle cx={size / 2} cy={size / 2} r={r} stroke={T.surface2} strokeWidth="3.5" fill="none" />
      <g style={{ transform: "rotate(-90deg)", transformOrigin: "center" }}>
        <circle
          cx={size / 2} cy={size / 2} r={r} stroke={color} strokeWidth="3.5" fill="none"
          strokeLinecap="round"
          strokeDasharray={circ}
          style={{ strokeDashoffset: circ * (1 - pct), transition: "stroke-dashoffset 500ms ease" }}
        />
      </g>
      {showNumber && (
        <text x={size / 2} y={size / 2 + size * 0.13} textAnchor="middle"
          style={{ ...numeric, fontSize: size * 0.34, fontWeight: 600 }} fill={rpe ? color : T.muted}>
          {rpe || "–"}
        </text>
      )}
    </svg>
  );
}

/* ============================== PERSISTENT STORE ==============================
   One hook owns everything that survives a reload: settings, equipment, logged
   sessions, PRs, favorites, and the weekly plan. Each concern gets its own key
   so a write to one never clobbers another, and every read is wrapped because a
   missing key throws rather than returning null on first run.
==================================================================== */
const STORE_KEYS = {
  settings: "ff-settings",
  equipment: "equipment-selection",
  history: "ff-history",
  prs: "ff-prs",
  favorites: "ff-favorites",
  savedWods: "ff-saved-wods",
  coachLog: "ff-coach-log",
  thread: "ff-coach-thread",
  acks: "ff-acks",
  metrics: "ff-metrics",
  plan: "ff-plan",
};

const DEFAULT_SETTINGS = { units: "kg", sound: true, voice: false, volume: 0.6, level: "intermediate", goal: "general", seenIntro: false };

async function loadKey(key, fallback) {
  try {
    const res = await window.storage?.get(key);
    return res?.value ? JSON.parse(res.value) : fallback;
  } catch (e) {
    return fallback;
  }
}
function saveKey(key, value) {
  try { window.storage?.set(key, JSON.stringify(value)).catch(() => {}); } catch (e) { /* offline, ignore */ }
}

function useStore() {
  const [settings, setSettings] = useState(DEFAULT_SETTINGS);
  const [equipment, setEquipment] = useState({ bodyweight: true });
  const [history, setHistory] = useState([]);
  const [prs, setPrs] = useState({});
  const [favorites, setFavorites] = useState([]);
  const [savedWods, setSavedWods] = useState([]);
  const [coachLog, setCoachLog] = useState([]);
  const [thread, setThread] = useState([]);
  const [acks, setAcks] = useState({});
  const [metrics, setMetrics] = useState([]);
  const [plan, setPlan] = useState(null);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    (async () => {
      setSettings(await loadKey(STORE_KEYS.settings, DEFAULT_SETTINGS));
      setEquipment(await loadKey(STORE_KEYS.equipment, { bodyweight: true }));
      setHistory(await loadKey(STORE_KEYS.history, []));
      setPrs(await loadKey(STORE_KEYS.prs, {}));
      setFavorites(await loadKey(STORE_KEYS.favorites, []));
      setSavedWods(await loadKey(STORE_KEYS.savedWods, []));
      setCoachLog(await loadKey(STORE_KEYS.coachLog, []));
      setThread(await loadKey(STORE_KEYS.thread, []));
      setAcks(await loadKey(STORE_KEYS.acks, {}));
      setMetrics(await loadKey(STORE_KEYS.metrics, []));
      setPlan(await loadKey(STORE_KEYS.plan, null));
      setLoaded(true);
    })();
  }, []);

  useEffect(() => { if (loaded) saveKey(STORE_KEYS.settings, settings); }, [settings, loaded]);
  // The sound engine is a module, not React state, so it has to be told.
  useEffect(() => { Sound.volume = settings.volume ?? 0.6; }, [settings.volume]);
  useEffect(() => { if (loaded) saveKey(STORE_KEYS.equipment, equipment); }, [equipment, loaded]);
  useEffect(() => { if (loaded) saveKey(STORE_KEYS.history, history); }, [history, loaded]);
  useEffect(() => { if (loaded) saveKey(STORE_KEYS.prs, prs); }, [prs, loaded]);
  useEffect(() => { if (loaded) saveKey(STORE_KEYS.favorites, favorites); }, [favorites, loaded]);
  useEffect(() => { if (loaded) saveKey(STORE_KEYS.savedWods, savedWods); }, [savedWods, loaded]);
  useEffect(() => { if (loaded) saveKey(STORE_KEYS.coachLog, coachLog); }, [coachLog, loaded]);
  useEffect(() => { if (loaded) saveKey(STORE_KEYS.thread, thread); }, [thread, loaded]);
  useEffect(() => { if (loaded) saveKey(STORE_KEYS.acks, acks); }, [acks, loaded]);
  useEffect(() => { if (loaded) saveKey(STORE_KEYS.metrics, metrics); }, [metrics, loaded]);
  useEffect(() => { if (loaded) saveKey(STORE_KEYS.plan, plan); }, [plan, loaded]);

  const logSession = (entry) =>
    setHistory((h) => [{ ...entry, id: Date.now(), date: new Date().toISOString() }, ...h].slice(0, 400));

  // Notes are the highest-signal thing the coach reads, so they stay editable
  // after the fact — you rarely write the useful version while still breathing hard.
  const updateSession = (id, patch) =>
    setHistory((h) => h.map((s) => (s.id === id ? { ...s, ...patch } : s)));

  const recordPr = (key, value, unit) =>
    setPrs((p) => {
      const prev = p[key];
      // Lower is better for times, higher is better for load/reps.
      const better = !prev || (unit === "time" ? value < prev.value : value > prev.value);
      return better ? { ...p, [key]: { value, unit, date: new Date().toISOString() } } : p;
    });

  const toggleFavorite = (id) =>
    setFavorites((f) => (f.includes(id) ? f.filter((x) => x !== id) : [...f, id]));

  // Starring a generated WOD has to store the whole thing — the title alone
  // can't be re-run, and generated workouts are never reproduced identically.
  // The coach's own past advice, so it can follow up rather than start fresh
  // every time. Kept short — only what's needed to hold a thread of intent.
  const recordAdvice = (entry) =>
    setCoachLog((l) => [{ ...entry, id: Date.now(), date: new Date().toISOString() }, ...l].slice(0, 40));

  const addMessage = (msg) => setThread((t) => [...t, { ...msg, at: Date.now() }].slice(-60));
  const clearThread = () => setThread([]);
  const acknowledge = (key) => setAcks((a) => ({ ...a, [key]: Date.now() }));

  // One entry per day: re-logging the same day overwrites rather than duplicating.
  const logMetrics = (entry) =>
    setMetrics((m) => {
      const day = new Date().toISOString().slice(0, 10);
      const rest = m.filter((x) => x.day !== day);
      return [{ ...entry, day, date: new Date().toISOString() }, ...rest].slice(0, 400);
    });

  const replaceAll = (data) => {
    if (data.history) setHistory(data.history);
    if (data.prs) setPrs(data.prs);
    if (data.metrics) setMetrics(data.metrics);
    if (data.favorites) setFavorites(data.favorites);
    if (data.savedWods) setSavedWods(data.savedWods);
    if (data.plan !== undefined) setPlan(data.plan);
    if (data.settings) setSettings({ ...DEFAULT_SETTINGS, ...data.settings });
  };

  const toggleSavedWod = (wod) =>
    setSavedWods((w) => {
      const key = wod.title + "|" + wod.meta;
      const exists = w.some((x) => x.key === key);
      return exists ? w.filter((x) => x.key !== key) : [{ ...wod, key, savedAt: Date.now() }, ...w].slice(0, 60);
    });

  return {
    settings, setSettings, equipment, setEquipment, history, setHistory,
    prs, recordPr, favorites, toggleFavorite, savedWods, toggleSavedWod, plan, setPlan, logSession, updateSession,
    coachLog, recordAdvice, thread, addMessage, clearThread, acks, acknowledge,
    metrics, logMetrics, replaceAll, loaded,
  };
}

/* ============================== UNITS ============================== */
const KG_TO_LB = 2.20462;
function displayWeight(kg, units) {
  if (units === "lb") return `${Math.round(kg * KG_TO_LB)} lb`;
  return `${Math.round(kg)} kg`;
}
// Prescriptions are authored in kg (e.g. "43/29kg"); rewrite them for lb users.
function convertLoadString(load, units) {
  if (!load || units !== "lb") return load;
  return load.replace(/(\d+(?:\.\d+)?)\s*\/\s*(\d+(?:\.\d+)?)\s*kg/gi,
    (_, a, b) => `${Math.round(a * KG_TO_LB)}/${Math.round(b * KG_TO_LB)} lb`)
    .replace(/(\d+(?:\.\d+)?)\s*kg/gi, (_, a) => `${Math.round(a * KG_TO_LB)} lb`);
}

/* ============================== SCALING ==============================
   Every movement gets a scale-down (easier / lower-skill / lighter) and a
   scale-up (harder / more load / more skill) option, keyed by pattern so
   the whole library is covered without hand-writing 250+ one-off lines.
==================================================================== */
const PATTERN_SCALE = {
  squat: { down: "Reduce depth, or hold something sturdy for balance.", up: "Add load, a pause at the bottom, or move to one leg." },
  hinge: { down: "Lighten the load and slow the tempo through the hinge.", up: "Add load, or add a deficit for more range of motion." },
  lunge: { down: "Shorten the range, or hold support for balance.", up: "Add load, or make it a jumping variation." },
  press: { down: "Use lighter weight, a band, or press from the knees.", up: "Add load, slow the tempo, or press single-arm." },
  pushup: { down: "Drop to the knees or elevate the hands on a box/bench.", up: "Elevate the feet, add a weight vest, or slow the descent." },
  pullup: { down: "Use a band for assistance, or do ring rows instead.", up: "Add a weight belt, slow the negative, or go strict." },
  row: { down: "Use lighter weight and focus on a clean pull.", up: "Add load, or pause each rep at full contraction." },
  jump: { down: "Step through instead of jumping, or reduce the height.", up: "Add a weight vest, or increase height/distance." },
  run: { down: "Reduce the distance, or walk part of the interval.", up: "Increase the pace, distance, or add an incline." },
  carry: { down: "Use lighter weight or shorten the distance.", up: "Increase the load or the distance." },
  swing: { down: "Use a lighter bell and a smaller range.", up: "Increase the weight or add more reps per set." },
  curl: { down: "Use lighter weight or a resistance band.", up: "Increase the load or slow the tempo." },
  calf: { down: "Reduce the range of motion, or sit down for support.", up: "Add load or move to a single leg." },
  core: { down: "Reduce reps, range of motion, or bend the knees.", up: "Add load, or slow the tempo way down." },
  plank: { down: "Drop to the knees to shorten the lever.", up: "Add a weight plate on the back, or lift one limb." },
};
function getScale(pattern, override) {
  return override || PATTERN_SCALE[pattern] || { down: "Reduce load, reps, or range of motion.", up: "Add load, reps, or slow the tempo." };
}

/* ============================== EXERCISE / MOVEMENT LIBRARY ============================== */
const EX = [
  { id: "burpee", name: "Burpees", equip: ["bodyweight"], muscle: ["Full Body", "Core"], pattern: "jump", unit: "reps", desc: "Chest to floor, jump up with a full hip extension overhead.", cues: ["Chest touches the ground", "Full extension at the top", "Land soft, reset fast"] },
  { id: "airsquat", name: "Air Squat", equip: ["bodyweight"], muscle: ["Quads", "Glutes"], pattern: "squat", unit: "reps", desc: "Bodyweight squat, hip crease below the knee.", cues: ["Knees track over toes", "Chest tall", "Hips back and down"] },
  { id: "pushup", name: "Push-ups", equip: ["bodyweight"], muscle: ["Chest", "Arms"], pattern: "pushup", unit: "reps", desc: "Chest to the deck, full lockout at the top.", cues: ["Body in one line", "Elbows ~45°", "Full lockout"] },
  { id: "pullup", name: "Pull-ups", equip: ["pull_up_bar"], muscle: ["Back", "Arms"], pattern: "pullup", prop: "bar", unit: "reps", desc: "Dead hang to chin over the bar.", cues: ["Active shoulders", "Chin clears the bar", "Control the descent"] },
  { id: "boxjump", name: "Box Jumps", equip: ["box"], muscle: ["Quads", "Glutes"], pattern: "jump", unit: "reps", desc: "Two-foot jump onto the box, stand tall at the top.", cues: ["Swing arms for momentum", "Land quiet", "Full hip extension on top"] },
  { id: "wallball", name: "Wall Balls", equip: ["wall_ball"], muscle: ["Quads", "Full Body"], pattern: "squat", unit: "reps", desc: "Squat and throw the ball to the target.", cues: ["Ball meets the target at full extension", "Squat below parallel", "Catch and go straight into the next squat"] },
  { id: "kbswing", name: "Kettlebell Swings", equip: ["kettlebell"], muscle: ["Glutes", "Hamstrings", "Full Body"], pattern: "swing", prop: "kettlebell", unit: "reps", desc: "Hip-hinge power swing to chest or eye height.", cues: ["Power from the hips, not the arms", "Flat back", "Snap the hips at the top"] },
  { id: "thruster", name: "Thrusters", equip: ["barbell", "dumbbells"], muscle: ["Full Body", "Quads", "Shoulders"], pattern: "press", prop: "barbell", unit: "reps", desc: "Front squat into a push press, one fluid motion.", cues: ["Elbows high in the squat", "Drive up out of the hole", "Punch overhead"] },
  { id: "snatch", name: "Power Snatch", equip: ["barbell"], muscle: ["Full Body"], pattern: "hinge", unit: "reps", desc: "Bar from floor to overhead in one pull.", cues: ["Bar stays close", "Aggressive hip extension", "Punch under the bar"], scale: { down: "Use an empty barbell or PVC pipe and drill the pull from the hip.", up: "Increase the load toward your snatch percentages, or add a pause at the knee." } },
  { id: "cleanjerk", name: "Clean & Jerk", equip: ["barbell"], muscle: ["Full Body"], pattern: "hinge", unit: "reps", desc: "Clean to the shoulders, jerk overhead.", cues: ["Elbows fast around the bar", "Solid front rack", "Split or push jerk overhead"], scale: { down: "Break it into a clean plus a strict press with a light bar.", up: "Increase the load, or add a 2-second pause in the front rack." } },
  { id: "deadlift", name: "Deadlift", equip: ["barbell"], muscle: ["Hamstrings", "Glutes", "Back"], pattern: "hinge", unit: "reps", desc: "Hip-hinge pull from the floor to lockout.", cues: ["Bar over midfoot", "Flat back throughout", "Push the floor away"], scale: { down: "Pull from blocks or a riser to shorten the range, with a lighter load.", up: "Increase the load, or add a 3-second pause just off the floor." } },
  { id: "backsquat", name: "Back Squat", equip: ["barbell", "squat_rack"], muscle: ["Quads", "Glutes"], pattern: "squat", prop: "barbell", unit: "reps", desc: "Bar on the back, squat to depth, drive up.", cues: ["Big breath, brace", "Knees out", "Drive through the whole foot"], scale: { down: "Use a lighter load and focus on depth and bracing.", up: "Increase the load, or add a 3-second pause at the bottom." } },
  { id: "frontsquat", name: "Front Squat", equip: ["barbell", "squat_rack"], muscle: ["Quads"], pattern: "squat", prop: "barbell", unit: "reps", desc: "Bar in the front rack, upright torso squat.", cues: ["Elbows up", "Stay vertical", "Sit between the heels"], scale: { down: "Use an empty bar or a goblet hold to learn the front rack.", up: "Increase the load, or add a pause out of the hole." } },
  { id: "ohp", name: "Overhead Press", equip: ["barbell", "dumbbells"], muscle: ["Shoulders", "Arms"], pattern: "press", prop: "barbell", unit: "reps", desc: "Strict press from shoulders to overhead lockout.", cues: ["Brace the core", "Bar path close to the face", "Lock out overhead"] },
  { id: "doubleunder", name: "Double-Unders", equip: ["jump_rope"], muscle: ["Full Body"], pattern: "jump", unit: "reps", cardio: true, desc: "Rope passes twice per jump.", cues: ["Jump low", "Wrists do the work", "Steady rhythm"], scale: { down: "Practice singles, or attempt doubles in small sets of 3-5.", up: "String bigger unbroken sets, or work toward triple-unders." } },
  { id: "row", name: "Row (Calories)", equip: ["rowing_machine"], muscle: ["Back"], pattern: "row", unit: "cal", cardio: true, desc: "Legs, hips, arms — then reverse.", cues: ["Legs drive first", "Lean back slightly", "Arms in last, out first"] },
  { id: "bike", name: "Assault Bike (Calories)", equip: ["assault_bike"], muscle: ["Quads"], pattern: "run", unit: "cal", cardio: true, desc: "Steady, hard pace on the bike.", cues: ["Push and pull with the arms", "Even pace", "Sit tall"] },
  { id: "situp", name: "Sit-ups", equip: ["bodyweight"], muscle: ["Core"], pattern: "core", unit: "reps", desc: "Full sit-up, shoulders to floor and back to touch toes.", cues: ["Anchor the feet", "Flat back down", "Fast transition up"] },
  { id: "toestobar", name: "Toes-to-Bar", equip: ["pull_up_bar"], muscle: ["Core"], pattern: "pullup", prop: "bar", unit: "reps", desc: "Hanging, toes touch the bar.", cues: ["Active shoulders on the hang", "Use the kip", "Toes to the bar, not just up"], scale: { down: "Do hanging knee raises, or knees-to-chest.", up: "Go strict toes-to-bar with no kip." } },
  { id: "hspu", name: "Handstand Push-ups", equip: ["bodyweight"], muscle: ["Shoulders"], pattern: "press", unit: "reps", desc: "Kick to a handstand, lower head to mat, press back up.", cues: ["Stack wrists under shoulders", "Control the descent", "Drive through the palms"], scale: { down: "Do a pike push-up with the feet elevated on a box.", up: "Add a deficit on parallettes, or wear a light weight vest." } },
  { id: "ringdip", name: "Ring Dips", equip: ["gymnastic_rings"], muscle: ["Chest", "Arms"], pattern: "press", unit: "reps", desc: "Dip on rings to full lockout.", cues: ["Rings stay close to the body", "Full lockout at top", "Control the bottom"], scale: { down: "Use a band for assistance, or do bar dips instead.", up: "Add a weight belt, or slow the descent to 3 seconds." } },
  { id: "sledpush", name: "Sled Push", equip: ["sled"], muscle: ["Quads", "Full Body"], pattern: "carry", unit: "m", desc: "Drive the sled with low, powerful steps.", cues: ["Low shin angle", "Drive with the legs", "Keep arms extended"] },
  { id: "battlerope", name: "Battle Rope Waves", equip: ["battle_rope"], muscle: ["Shoulders", "Core"], pattern: "swing", unit: "sec", cardio: true, desc: "Alternating waves, stay low in an athletic stance.", cues: ["Athletic stance", "Waves from the shoulders", "Keep a steady rhythm"] },
  { id: "sandbagcarry", name: "Sandbag Carry", equip: ["sandbag"], muscle: ["Full Body", "Core"], pattern: "carry", unit: "m", desc: "Carry loaded, braced core, steady pace.", cues: ["Brace before you lift", "Short, controlled steps", "Don't lean back"] },
  { id: "barbellrow", name: "Barbell Row", equip: ["barbell"], muscle: ["Back"], pattern: "row", prop: "barbell", unit: "reps", desc: "Hinge over, row the bar to the lower ribs.", cues: ["Flat back", "Pull with the elbows", "No swinging"] },
  { id: "dbrow", name: "Dumbbell Row", equip: ["dumbbells", "bench"], muscle: ["Back"], pattern: "row", prop: "dumbbell", unit: "reps", desc: "One arm at a time, braced on a bench.", cues: ["Flat back", "Row to the hip", "Squeeze at the top"] },
  { id: "lunge", name: "Lunges", equip: ["bodyweight", "dumbbells"], muscle: ["Quads", "Glutes"], pattern: "lunge", unit: "reps", desc: "Step forward or back into a knee-friendly lunge.", cues: ["Front knee over the ankle", "Torso upright", "Push through the front heel"] },
  { id: "rdl", name: "Romanian Deadlift", equip: ["barbell", "dumbbells"], muscle: ["Hamstrings", "Glutes"], pattern: "hinge", unit: "reps", desc: "Hinge with a slight knee bend, feel the hamstrings load.", cues: ["Bar close to the legs", "Flat back", "Hips back, not down"] },
  { id: "hipthrust", name: "Hip Thrust", equip: ["barbell", "bench"], muscle: ["Glutes"], pattern: "hinge", unit: "reps", desc: "Shoulders on the bench, drive the hips up.", cues: ["Chin tucked", "Squeeze glutes at the top", "Ribs down"] },
  { id: "bench", name: "Bench Press", equip: ["barbell", "bench"], muscle: ["Chest"], pattern: "pushup", prop: "barbell", unit: "reps", desc: "Bar to the chest, press to lockout.", cues: ["Shoulder blades pinched", "Bar path slightly toward the face", "Feet driving into the floor"] },
  { id: "curl", name: "Bicep Curl", equip: ["dumbbells"], muscle: ["Arms"], pattern: "curl", prop: "dumbbell", unit: "reps", desc: "Strict curl, no swinging.", cues: ["Elbows pinned", "Full range", "Control the negative"] },
  { id: "tricepdip", name: "Tricep Dip", equip: ["dip_bars", "bench"], muscle: ["Arms"], pattern: "press", unit: "reps", desc: "Lower under control, press back to lockout.", cues: ["Elbows track back", "Shoulders down", "Full lockout"] },
  { id: "plank", name: "Plank Hold", equip: ["bodyweight"], muscle: ["Core"], pattern: "plank", unit: "sec", desc: "Forearm plank, straight line head to heel.", cues: ["Squeeze glutes", "Ribs down", "Don't let hips sag"] },
  { id: "mountainclimber", name: "Mountain Climbers", equip: ["bodyweight"], muscle: ["Core"], pattern: "core", unit: "reps", desc: "Drive knees to chest from a plank.", cues: ["Hips stay level", "Fast feet", "Hands under shoulders"] },
  { id: "calfraise", name: "Calf Raise", equip: ["bodyweight", "dumbbells"], muscle: ["Quads"], pattern: "calf", unit: "reps", desc: "Rise onto the toes, squeeze, lower slow.", cues: ["Full range", "Pause at the top", "Control the descent"] },
  { id: "run400", name: "Run", equip: ["space_to_run"], muscle: ["Full Body"], pattern: "run", unit: "m", cardio: true, desc: "Steady or hard-pace running interval.", cues: ["Relaxed shoulders", "Quick cadence", "Breathe rhythmically"] },
  { id: "gobletsquat", name: "Goblet Squat", equip: ["dumbbells", "kettlebell"], muscle: ["Quads", "Glutes"], pattern: "squat", prop: "dumbbell", unit: "reps", desc: "Hold a weight at chest height, squat between the knees.", cues: ["Elbows brush the knees at the bottom", "Chest tall", "Drive through mid-foot"] },
  { id: "bulgariansplitsquat", name: "Bulgarian Split Squat", equip: ["dumbbells", "bench"], muscle: ["Quads", "Glutes"], pattern: "lunge", prop: "dumbbell", unit: "reps", desc: "Rear foot elevated, drop straight down into the front leg.", cues: ["Front shin stays fairly vertical", "Control the descent", "Drive through the front heel"] },
  { id: "pushpress", name: "Push Press", equip: ["barbell"], muscle: ["Shoulders", "Full Body"], pattern: "press", prop: "barbell", unit: "reps", desc: "A small dip and drive from the legs launches the bar overhead.", cues: ["Short, fast dip", "Punch through at lockout", "Bar path stays close"], scale: { down: "Lighten the bar and focus on the dip-drive timing.", up: "Increase the load, or switch to strict press once fatigued." } },
  { id: "pushjerk", name: "Push Jerk", equip: ["barbell"], muscle: ["Full Body"], pattern: "press", prop: "barbell", unit: "reps", desc: "Dip, drive, and re-bend the knees to catch the bar overhead.", cues: ["Aggressive leg drive", "Catch with locked-out arms", "Stand tall to finish"], scale: { down: "Practice the dip and catch with an empty bar first.", up: "Increase the load toward a heavy single or double." } },
  { id: "pistolsquat", name: "Pistol Squat", equip: ["bodyweight"], muscle: ["Quads", "Glutes"], pattern: "squat", unit: "reps", desc: "Single-leg squat, other leg extended in front.", cues: ["Sit back like reaching for a chair", "Keep the heel down", "Use arms to counterbalance"], scale: { down: "Hold a rig or TRX for balance, or box down to a bench.", up: "Hold a dumbbell at the chest, or slow the descent to 5 seconds." } },
  { id: "wallsit", name: "Wall Sit", equip: ["bodyweight"], muscle: ["Quads"], pattern: "squat", unit: "sec", desc: "Back against the wall, thighs parallel to the floor, hold.", cues: ["Knees over ankles", "Press the low back flat", "Breathe steadily"] },
  { id: "stepup", name: "Step-ups", equip: ["box", "dumbbells"], muscle: ["Quads", "Glutes"], pattern: "lunge", unit: "reps", desc: "Drive up through the lead leg onto the box, stand tall.", cues: ["Full foot on the box", "Drive through the heel", "Control the step down"] },
  { id: "glutebridge", name: "Glute Bridge", equip: ["bodyweight", "barbell"], muscle: ["Glutes"], pattern: "hinge", unit: "reps", desc: "Lying on your back, drive the hips up, squeeze at the top.", cues: ["Ribs stay down", "Squeeze glutes hard at the top", "Control the lower"] },
  { id: "goodmorning", name: "Good Morning", equip: ["barbell"], muscle: ["Hamstrings", "Back"], pattern: "hinge", prop: "barbell", unit: "reps", desc: "Bar on the back, hinge forward with a soft knee bend.", cues: ["Flat back", "Hips hinge back", "Stop when the hamstrings load up"] },
  { id: "farmerscarry", name: "Farmer's Carry", equip: ["dumbbells", "kettlebell"], muscle: ["Full Body", "Core"], pattern: "carry", prop: "dumbbell", unit: "m", desc: "A heavy weight in each hand, walk tall with braced steps.", cues: ["Shoulders back", "Braced core", "Short, controlled steps"] },
  { id: "renegaderow", name: "Renegade Row", equip: ["dumbbells"], muscle: ["Back", "Core"], pattern: "row", prop: "dumbbell", unit: "reps", desc: "From a plank, row one dumbbell at a time without rotating the hips.", cues: ["Hips stay square", "Wide base with the feet", "Row to the hip"] },
  { id: "broadjump", name: "Broad Jump", equip: ["bodyweight"], muscle: ["Quads", "Glutes", "Full Body"], pattern: "jump", unit: "reps", desc: "Jump forward for maximum distance, stick the landing.", cues: ["Swing the arms hard", "Land soft with bent knees", "Reset fully between reps"] },
  { id: "tuckjump", name: "Tuck Jump", equip: ["bodyweight"], muscle: ["Quads", "Full Body"], pattern: "jump", unit: "reps", desc: "Explosive vertical jump, knees drive up toward the chest.", cues: ["Quick ground contact", "Knees high", "Land soft"] },
  { id: "bearcrawl", name: "Bear Crawl", equip: ["bodyweight"], muscle: ["Full Body", "Core"], pattern: "carry", unit: "m", desc: "Hands and feet on the ground, knees hovering, crawl forward.", cues: ["Opposite hand and foot move together", "Hips low", "Braced core"] },
  { id: "superman", name: "Superman", equip: ["bodyweight"], muscle: ["Core", "Back"], pattern: "core", unit: "reps", desc: "Lying face down, lift chest and legs together, hold, lower.", cues: ["Reach long through the fingers and toes", "Squeeze the glutes", "Lift without straining the neck"] },
  { id: "skullcrusher", name: "Skull Crusher", equip: ["dumbbells", "barbell", "bench"], muscle: ["Arms"], pattern: "pushup", prop: "barbell", unit: "reps", desc: "Lying down, lower the weight toward the forehead, extend back up.", cues: ["Elbows stay pointed at the ceiling", "Control the lower", "Full extension at the top"] },
  { id: "lateralraise", name: "Lateral Raise", equip: ["dumbbells"], muscle: ["Shoulders"], pattern: "curl", prop: "dumbbell", unit: "reps", desc: "Raise the weights out to the sides to shoulder height.", cues: ["Slight bend in the elbows", "Lead with the elbows", "Control the descent"] },
  { id: "facepull", name: "Face Pull", equip: ["bands"], muscle: ["Shoulders", "Back"], pattern: "row", unit: "reps", desc: "Pull the band toward the face, elbows high, squeeze the shoulder blades.", cues: ["Elbows above the hands", "Squeeze the shoulder blades", "Slow release"] },
  { id: "devilpress", name: "Devil Press", equip: ["dumbbells"], muscle: ["Full Body"], pattern: "jump", prop: "dumbbell", unit: "reps", desc: "Burpee with dumbbells into a double dumbbell snatch overhead.", cues: ["Chest to the floor", "Explosive hip drive", "Lockout overhead"] },

  // Chest
  { id: "inclinebench", name: "Incline Bench Press", equip: ["barbell", "bench"], muscle: ["Chest"], pattern: "pushup", prop: "barbell", unit: "reps", desc: "Bench set to an incline, press the bar up and slightly back.", cues: ["Shoulder blades pinched", "Bar to the upper chest", "Drive the feet down"] },
  { id: "declinebench", name: "Decline Bench Press", equip: ["barbell", "bench"], muscle: ["Chest"], pattern: "pushup", prop: "barbell", unit: "reps", desc: "Bench set to a decline, press the bar up over the lower chest.", cues: ["Secure the legs", "Control the descent", "Full lockout"] },
  { id: "dbfly", name: "Dumbbell Fly", equip: ["dumbbells", "bench"], muscle: ["Chest"], pattern: "pushup", unit: "reps", desc: "Wide arc from the chest up and over, slight bend in the elbows.", cues: ["Soft elbow bend throughout", "Feel the stretch at the bottom", "Squeeze at the top"] },
  { id: "cablecrossover", name: "Cable Crossover", equip: ["cable_machine"], muscle: ["Chest"], pattern: "press", unit: "reps", desc: "Standing between towers, sweep both handles down and together.", cues: ["Slight forward lean", "Lead with the wrists", "Squeeze at the finish"] },
  { id: "chestdips", name: "Chest Dips", equip: ["dip_bars"], muscle: ["Chest", "Arms"], pattern: "press", unit: "reps", desc: "Lean forward on the dip bars to bias the chest.", cues: ["Torso leans forward", "Elbows flare slightly", "Control the bottom"] },
  { id: "pecdeck", name: "Pec Deck / Machine Fly", equip: ["cable_machine"], muscle: ["Chest"], pattern: "pushup", unit: "reps", desc: "Seated machine fly, bring the pads together in front of the chest.", cues: ["Squeeze and pause at the front", "Control the release", "Keep shoulders down"] },
  { id: "landminepress", name: "Landmine Press", equip: ["landmine"], muscle: ["Chest", "Shoulders"], pattern: "press", unit: "reps", desc: "One end of the bar anchored, press the other end up and forward.", cues: ["Brace the core", "Press along the natural arc", "Control the return"] },

  // Back
  { id: "latpulldown", name: "Lat Pulldown", equip: ["cable_machine"], muscle: ["Back", "Arms"], pattern: "pullup", unit: "reps", desc: "Seated, pull the bar down to the upper chest.", cues: ["Lead with the elbows", "Chest tall", "Control the return"] },
  { id: "seatedcablerow", name: "Seated Cable Row", equip: ["cable_machine"], muscle: ["Back"], pattern: "row", unit: "reps", desc: "Seated, row the handle to the torso, squeeze the shoulder blades.", cues: ["Chest up", "Elbows close to the body", "Full stretch forward"] },
  { id: "tbarrow", name: "T-Bar Row", equip: ["barbell", "landmine"], muscle: ["Back"], pattern: "row", prop: "barbell", unit: "reps", desc: "Straddle the bar, hinge forward, row to the chest.", cues: ["Flat back", "Pull with the elbows", "Squeeze at the top"] },
  { id: "pullover", name: "Dumbbell Pullover", equip: ["dumbbells", "bench"], muscle: ["Back", "Chest"], pattern: "press", unit: "reps", desc: "Lying on a bench, lower a weight behind the head, pull back over the chest.", cues: ["Ribs stay down", "Long arc through the shoulders", "Control the stretch"] },
  { id: "invertedrow", name: "Inverted Row", equip: ["squat_rack", "pull_up_bar"], muscle: ["Back", "Arms"], pattern: "row", unit: "reps", desc: "Body under a bar, pull the chest up to it, body stays straight.", cues: ["Straight line head to heel", "Squeeze the shoulder blades", "Control the lower"] },
  { id: "rackpull", name: "Rack Pull", equip: ["barbell", "squat_rack"], muscle: ["Back", "Hamstrings"], pattern: "hinge", prop: "barbell", unit: "reps", desc: "Partial deadlift from pins set just below the knee.", cues: ["Flat back", "Drive the hips through", "Squeeze at lockout"], scale: { down: "Set the pins higher and use a lighter load.", up: "Lower the pin height, or increase the load." } },

  // Shoulders
  { id: "arnoldpress", name: "Arnold Press", equip: ["dumbbells"], muscle: ["Shoulders"], pattern: "press", prop: "dumbbell", unit: "reps", desc: "Rotate the palms out while pressing overhead.", cues: ["Start palms facing you", "Rotate as you press", "Full lockout overhead"] },
  { id: "frontraise", name: "Front Raise", equip: ["dumbbells"], muscle: ["Shoulders"], pattern: "curl", prop: "dumbbell", unit: "reps", desc: "Raise the weights straight out in front to shoulder height.", cues: ["Slight bend in the elbows", "No swinging or momentum", "Lower with control"] },
  { id: "reardeltfly", name: "Rear Delt Fly", equip: ["dumbbells"], muscle: ["Shoulders", "Back"], pattern: "row", prop: "dumbbell", unit: "reps", desc: "Hinged forward, raise the weights out to the sides.", cues: ["Soft knees, flat back", "Lead with the elbows", "Squeeze the shoulder blades"] },
  { id: "uprightrow", name: "Upright Row", equip: ["barbell", "dumbbells"], muscle: ["Shoulders"], pattern: "curl", prop: "barbell", unit: "reps", desc: "Pull the weight up along the body to chest height, elbows lead.", cues: ["Elbows above the wrists", "Keep the bar close", "Control the descent"] },
  { id: "shrugs", name: "Barbell Shrugs", equip: ["barbell", "dumbbells"], muscle: ["Shoulders"], pattern: "carry", prop: "barbell", unit: "reps", desc: "Shrug the shoulders straight up toward the ears, hold, release.", cues: ["No rolling — straight up and down", "Pause at the top", "Control the release"] },
  { id: "pikepushup", name: "Pike Push-up", equip: ["bodyweight"], muscle: ["Shoulders"], pattern: "pushup", unit: "reps", desc: "Hips high, lower the head toward the floor between the hands.", cues: ["Hips stay high", "Crown of the head toward the floor", "Press back to start"] },

  // Arms
  { id: "hammercurl", name: "Hammer Curl", equip: ["dumbbells"], muscle: ["Arms"], pattern: "curl", prop: "dumbbell", unit: "reps", desc: "Curl with a neutral, palms-in grip.", cues: ["Elbows pinned to the sides", "No swinging", "Control the negative"] },
  { id: "concentrationcurl", name: "Concentration Curl", equip: ["dumbbells"], muscle: ["Arms"], pattern: "curl", prop: "dumbbell", unit: "reps", desc: "Elbow braced against the inner thigh, curl the weight up strictly.", cues: ["Elbow stays planted", "Squeeze at the top", "Slow negative"] },
  { id: "preachercurl", name: "Preacher Curl", equip: ["dumbbells", "barbell", "bench"], muscle: ["Arms"], pattern: "curl", prop: "barbell", unit: "reps", desc: "Arms braced on the preacher pad, curl through a strict range.", cues: ["Don't lock out fully at the bottom", "Squeeze at the top", "Control the lower"] },
  { id: "closegripbench", name: "Close-Grip Bench Press", equip: ["barbell", "bench"], muscle: ["Arms", "Chest"], pattern: "pushup", prop: "barbell", unit: "reps", desc: "Narrow grip bench press that biases the triceps.", cues: ["Elbows track close to the body", "Bar to the lower chest", "Full lockout"] },
  { id: "overheadtricep", name: "Overhead Tricep Extension", equip: ["dumbbells"], muscle: ["Arms"], pattern: "press", prop: "dumbbell", unit: "reps", desc: "Weight overhead, lower behind the head, extend back up.", cues: ["Elbows stay narrow and still", "Full stretch at the bottom", "Squeeze at lockout"] },
  { id: "cablepushdown", name: "Cable Pushdown", equip: ["cable_machine"], muscle: ["Arms"], pattern: "curl", unit: "reps", desc: "Elbows pinned at the sides, press the bar down to full extension.", cues: ["Elbows stay glued to the sides", "Full extension at the bottom", "Control the return"] },
  { id: "zottmancurl", name: "Zottman Curl", equip: ["dumbbells"], muscle: ["Arms"], pattern: "curl", prop: "dumbbell", unit: "reps", desc: "Curl up palms-up, rotate, lower palms-down.", cues: ["Rotate at the top", "Slow controlled lower", "No swinging"] },

  // Quads / legs
  { id: "legpress", name: "Leg Press", equip: ["leg_press"], muscle: ["Quads", "Glutes"], pattern: "squat", unit: "reps", desc: "Seated in the machine, press the platform away, control the return.", cues: ["Feet shoulder width on the platform", "Don't lock the knees out hard", "Full range without the low back rounding"] },
  { id: "legextension", name: "Leg Extension", equip: ["leg_press"], muscle: ["Quads"], pattern: "squat", unit: "reps", desc: "Seated machine, extend the knees to full lockout, control the return.", cues: ["Pause at the top", "Control the negative", "Avoid swinging the weight up"] },
  { id: "hacksquat", name: "Hack Squat", equip: ["leg_press"], muscle: ["Quads", "Glutes"], pattern: "squat", unit: "reps", desc: "Machine-guided squat with the back supported.", cues: ["Full foot contact", "Knees track over the toes", "Controlled depth"] },
  { id: "sissysquat", name: "Sissy Squat", equip: ["bodyweight"], muscle: ["Quads"], pattern: "squat", unit: "reps", desc: "Lean back from the knees while bending, keeping the hips extended.", cues: ["Hold onto something for balance", "Knees travel forward", "Control the lean-back"], scale: { down: "Hold onto a rig or post for balance and use a partial range.", up: "Hold a weight behind the head, or go full range unassisted." } },
  { id: "boxsquat", name: "Box Squat", equip: ["barbell", "box", "squat_rack"], muscle: ["Quads", "Glutes"], pattern: "squat", prop: "barbell", unit: "reps", desc: "Squat to a box, pause, then drive back up.", cues: ["Sit back to the box", "Brief pause, no relaxing", "Drive up through the whole foot"] },
  { id: "zerchersquat", name: "Zercher Squat", equip: ["barbell"], muscle: ["Quads", "Full Body"], pattern: "squat", prop: "barbell", unit: "reps", desc: "Bar cradled in the elbows, squat to depth.", cues: ["Keep the torso upright", "Elbows high", "Brace hard through the midline"], scale: { down: "Use a lighter bar and pad the elbows well.", up: "Increase the load, or pause 2 seconds at depth." } },
  { id: "sumosquat", name: "Sumo Squat", equip: ["dumbbells", "kettlebell"], muscle: ["Quads", "Glutes"], pattern: "squat", prop: "dumbbell", unit: "reps", desc: "Wide stance, toes turned out, squat straight down.", cues: ["Knees track over the toes", "Chest tall", "Squeeze glutes at the top"] },
  { id: "jumpsquat", name: "Jump Squat", equip: ["bodyweight"], muscle: ["Quads", "Glutes", "Full Body"], pattern: "jump", unit: "reps", desc: "Squat down and explode straight up into a jump.", cues: ["Full squat depth", "Explosive drive", "Land soft and reset"] },

  // Hamstrings
  { id: "lyinglegcurl", name: "Lying Leg Curl", equip: ["leg_press"], muscle: ["Hamstrings"], pattern: "core", unit: "reps", desc: "Face down on the machine, curl the heels toward the glutes.", cues: ["Hips stay pressed down", "Squeeze at the top", "Control the release"] },
  { id: "nordiccurl", name: "Nordic Curl", equip: ["bodyweight"], muscle: ["Hamstrings"], pattern: "hinge", unit: "reps", desc: "Ankles anchored, lower the torso forward under control, hamstrings resist the whole way.", cues: ["Keep the hips extended", "Lower as slow as possible", "Use the hands to catch if needed"], scale: { down: "Anchor a band in front for assistance, or use a very small range of motion.", up: "Hold a weight at the chest, or eliminate the hand-catch at the bottom." } },
  { id: "singlelegrdl", name: "Single-Leg RDL", equip: ["dumbbells", "kettlebell"], muscle: ["Hamstrings", "Glutes"], pattern: "hinge", prop: "dumbbell", unit: "reps", desc: "Balance on one leg, hinge forward as the other leg extends back.", cues: ["Hips stay square", "Reach the weight toward the floor", "Soft bend in the standing knee"] },
  { id: "kbdeadlift", name: "Kettlebell Deadlift", equip: ["kettlebell"], muscle: ["Hamstrings", "Glutes", "Back"], pattern: "hinge", prop: "kettlebell", unit: "reps", desc: "Hinge down to the kettlebell between the feet, stand tall.", cues: ["Flat back", "Hips back first", "Stand up tall at the top"] },
  { id: "cablepullthrough", name: "Cable Pull-Through", equip: ["cable_machine"], muscle: ["Hamstrings", "Glutes"], pattern: "hinge", unit: "reps", desc: "Facing away from the cable, hinge forward and drive the hips through.", cues: ["Rope stays between the legs", "Hinge, don't squat", "Snap the hips forward"] },

  // Glutes
  { id: "cablekickback", name: "Cable Kickback", equip: ["cable_machine"], muscle: ["Glutes"], pattern: "lunge", unit: "reps", desc: "Cuff on the ankle, kick the leg straight back and squeeze.", cues: ["Slight forward lean", "Squeeze the glute at the top", "Control the return"] },
  { id: "curtsylunge", name: "Curtsy Lunge", equip: ["bodyweight", "dumbbells"], muscle: ["Glutes", "Quads"], pattern: "lunge", unit: "reps", desc: "Step one leg diagonally behind the other and bend both knees.", cues: ["Torso stays upright", "Front knee tracks over the foot", "Push back to standing"] },
  { id: "bandedwalk", name: "Banded Lateral Walk", equip: ["bands"], muscle: ["Glutes"], pattern: "squat", unit: "reps", desc: "Band around the knees or ankles, step sideways in an athletic stance.", cues: ["Stay low the whole time", "Keep tension on the band", "Small controlled steps"] },
  { id: "hipabduction", name: "Hip Abduction Machine", equip: ["leg_press"], muscle: ["Glutes"], pattern: "squat", unit: "reps", desc: "Seated machine, push the knees outward against resistance.", cues: ["Controlled tempo", "Squeeze at the widest point", "Avoid bouncing"] },
  { id: "singlelegbridge", name: "Single-Leg Glute Bridge", equip: ["bodyweight"], muscle: ["Glutes"], pattern: "hinge", unit: "reps", desc: "One foot on the floor, drive the hips up, other leg extended.", cues: ["Hips stay level", "Squeeze at the top", "Control the lower"] },
  { id: "frogpump", name: "Frog Pump", equip: ["bodyweight", "dumbbells"], muscle: ["Glutes"], pattern: "hinge", unit: "reps", desc: "Soles of the feet together, knees out, pulse the hips up.", cues: ["Small, controlled range", "Squeeze hard at the top", "Keep the soles pressed together"] },

  // Core
  { id: "hanginglegraise", name: "Hanging Leg Raise", equip: ["pull_up_bar"], muscle: ["Core"], pattern: "pullup", prop: "bar", unit: "reps", desc: "Hanging from the bar, raise the legs to hip height or higher.", cues: ["Avoid swinging", "Control the lower", "Posterior pelvic tilt at the top"] },
  { id: "russiantwist", name: "Russian Twist", equip: ["bodyweight", "dumbbells"], muscle: ["Core"], pattern: "core", unit: "reps", desc: "Seated, lean back slightly, rotate a weight side to side.", cues: ["Feet can stay down for an easier variation", "Rotate through the torso", "Controlled tempo"] },
  { id: "cablewoodchop", name: "Cable Woodchop", equip: ["cable_machine"], muscle: ["Core"], pattern: "swing", unit: "reps", desc: "Pull the cable diagonally across the body, high to low or low to high.", cues: ["Rotate through the torso", "Arms stay fairly straight", "Control the return"] },
  { id: "abwheelrollout", name: "Ab Wheel Rollout", equip: ["ab_wheel"], muscle: ["Core"], pattern: "plank", unit: "reps", desc: "Kneeling, roll the wheel out as far as control allows, pull back in.", cues: ["Brace hard before moving", "Avoid letting the hips sag", "Only go as far as you can control"] },
  { id: "deadbug", name: "Dead Bug", equip: ["bodyweight"], muscle: ["Core"], pattern: "core", unit: "reps", desc: "Lying on your back, extend opposite arm and leg while the low back stays flat.", cues: ["Low back pressed into the floor", "Move slowly", "Opposite arm and leg together"] },
  { id: "hollowhold", name: "Hollow Hold", equip: ["bodyweight"], muscle: ["Core"], pattern: "plank", unit: "sec", desc: "Lying on your back, lift the shoulders and legs, low back pressed down.", cues: ["Low back stays flat", "Reach long through fingers and toes", "Breathe steadily"] },
  { id: "vup", name: "V-Up", equip: ["bodyweight"], muscle: ["Core"], pattern: "core", unit: "reps", desc: "Fold at the middle, hands reaching for the toes, then lower with control.", cues: ["Legs and torso lift together", "Reach past the toes", "Control the lower"] },
  { id: "sideplank", name: "Side Plank", equip: ["bodyweight"], muscle: ["Core"], pattern: "plank", unit: "sec", desc: "Balance on one forearm, body in a straight line, hips lifted.", cues: ["Stack the hips", "Don't let the hips sag", "Long line head to feet"] },
  { id: "pallofpress", name: "Pallof Press", equip: ["bands", "cable_machine"], muscle: ["Core"], pattern: "press", unit: "reps", desc: "Standing sideways to the anchor, press the handle straight out and resist rotating.", cues: ["Resist the twist", "Brace before pressing", "Slow controlled return"] },

  // Full Body / CrossFit / Strongman
  { id: "wallwalk", name: "Wall Walk", equip: ["bodyweight"], muscle: ["Full Body", "Shoulders"], pattern: "press", unit: "reps", desc: "From a plank, walk the feet up the wall while walking the hands in.", cues: ["Small controlled steps", "Keep the core braced", "Walk back down with control"], scale: { down: "Hold a shoulder-taps plank facing the wall instead of walking up it.", up: "Walk higher up the wall, or add a second ascent." } },
  { id: "ropeclimb", name: "Rope Climb", equip: ["rope_to_climb"], muscle: ["Full Body", "Back", "Arms"], pattern: "pullup", unit: "reps", desc: "Climb the rope hand over hand using the legs to assist.", cues: ["Lock the rope with the feet", "Pull with the lats", "Controlled descent"], scale: { down: "Do seated rope pulls, or use the legs (J-hook) to assist the climb.", up: "Go legless, or add an extra ascent to the workout." } },
  { id: "sleddrag", name: "Sled Drag", equip: ["sled"], muscle: ["Full Body", "Quads"], pattern: "carry", unit: "m", desc: "Harness or rope attached to the sled, walk backward or forward dragging it.", cues: ["Stay low and driven", "Steady, even steps", "Keep tension on the strap"], scale: { down: "Reduce the weight or shorten the distance.", up: "Increase the weight, or switch to a harness sprint." } },
  { id: "tireflip", name: "Tire Flip", equip: ["tire"], muscle: ["Full Body"], pattern: "hinge", unit: "reps", desc: "Hinge down, drive through the legs and hips to flip the tire over.", cues: ["Flat back on the pickup", "Drive with the legs", "Follow through with the hips"], scale: { down: "Use a smaller, lighter tire.", up: "Use a heavier tire, or add a sprint after each flip." } },
  { id: "atlasstone", name: "Atlas Stone Lift", equip: ["atlas_stone"], muscle: ["Full Body", "Back"], pattern: "hinge", unit: "reps", desc: "Lift the stone from the ground to the chest or over a platform.", cues: ["Get the stone close first", "Round the back only as trained", "Use the legs to finish"], scale: { down: "Use a lighter stone, or sub a sandbag.", up: "Increase the stone weight, or raise the platform height." } },
  { id: "yokecarry", name: "Yoke Carry", equip: ["yoke"], muscle: ["Full Body"], pattern: "carry", unit: "m", desc: "Load on the shoulders, walk for distance under control.", cues: ["Brace hard before stepping off", "Quick, controlled steps", "Stay tall"], scale: { down: "Reduce the load or shorten the distance.", up: "Increase the load, or add distance or an incline." } },
  { id: "dballclean", name: "D-Ball Clean", equip: ["atlas_stone"], muscle: ["Full Body"], pattern: "hinge", unit: "reps", desc: "Explosively bring the ball from the floor to the shoulder.", cues: ["Keep the ball close", "Drive with the hips", "Catch it high on the chest"] },
  { id: "snatchbalance", name: "Snatch Balance", equip: ["barbell"], muscle: ["Full Body", "Shoulders"], pattern: "press", prop: "barbell", unit: "reps", desc: "Drop under a bar from the back rack into an overhead squat.", cues: ["Fast drop under the bar", "Punch the arms straight", "Stay stable in the bottom"], scale: { down: "Use an empty bar and drill the footwork without dropping fast.", up: "Increase the load toward your heaviest overhead squat." } },
  { id: "overheadsquat", name: "Overhead Squat", equip: ["barbell"], muscle: ["Full Body", "Quads", "Shoulders"], pattern: "squat", prop: "barbell", unit: "reps", desc: "Bar locked out overhead, squat to full depth without losing position.", cues: ["Bar stays over the mid-foot", "Active shoulders throughout", "Sit tall through the bottom"], scale: { down: "Use a PVC pipe or empty bar and focus on mobility.", up: "Increase the load toward your snatch weight." } },
  { id: "muscleup", name: "Muscle-Up", equip: ["gymnastic_rings"], muscle: ["Full Body", "Back", "Arms"], pattern: "pullup", unit: "reps", desc: "Pull high enough to transition over the rings into a dip lockout.", cues: ["False grip on the rings", "Aggressive pull to the transition", "Finish with a strong dip"], scale: { down: "Practice with a band, or do a strict pull-up plus a ring dip.", up: "Slow the transition, or remove the kip entirely." } },
  { id: "barmuscleup", name: "Bar Muscle-Up", equip: ["pull_up_bar"], muscle: ["Full Body", "Back", "Arms"], pattern: "pullup", prop: "bar", unit: "reps", desc: "Pull and lean forward over the bar into a supported lockout.", cues: ["Big kip or strict pull", "Lean forward over the bar", "Press to lockout"], scale: { down: "Sub a strict pull-up plus a bar dip for each rep.", up: "Go strict with no kip, or add a slow 3-second negative." } },
  { id: "jumpinglunge", name: "Jumping Lunge", equip: ["bodyweight"], muscle: ["Quads", "Glutes", "Full Body"], pattern: "jump", unit: "reps", desc: "Lunge position, jump and switch legs in the air.", cues: ["Land soft in the lunge", "Stay tall through the torso", "Quick, light switches"] },
  { id: "boxstepover", name: "Box Step-Over", equip: ["box"], muscle: ["Full Body", "Quads"], pattern: "lunge", unit: "reps", desc: "Step up and completely over the box to the other side.", cues: ["Full foot on the box", "Stand tall at the top", "Control the step down"] },
  { id: "sumodlhighpull", name: "Sumo Deadlift High Pull", equip: ["barbell", "kettlebell"], muscle: ["Full Body"], pattern: "hinge", prop: "barbell", unit: "reps", desc: "Wide-stance deadlift that finishes by pulling the bar to the chin.", cues: ["Wide stance, toes out", "Explosive hip extension", "Elbows high and outside"] },
  { id: "medicineballslam", name: "Medicine Ball Slam", equip: ["wall_ball"], muscle: ["Full Body", "Core"], pattern: "swing", unit: "reps", desc: "Lift the ball overhead and slam it down as hard as possible.", cues: ["Full extension overhead", "Slam with the hips and core", "Reset your stance each rep"] },
  { id: "manmaker", name: "Man Maker", equip: ["dumbbells"], muscle: ["Full Body"], pattern: "press", prop: "dumbbell", unit: "reps", desc: "Push-up and row combo into a clean and press.", cues: ["Row each side before standing", "Clean the weights to the shoulders", "Press to full lockout"] },
  { id: "turkishgetup", name: "Turkish Get-Up", equip: ["kettlebell"], muscle: ["Full Body", "Core", "Shoulders"], pattern: "press", prop: "kettlebell", unit: "reps", desc: "From lying to standing while keeping a weight locked out overhead.", cues: ["Eyes on the weight the whole time", "Move through each position slowly", "Stack the joints overhead"], scale: { down: "Use a light kettlebell, or just a shoe balanced on the fist to learn the pattern.", up: "Increase the load, or slow each transition to a 3-second count." } },
  { id: "shuttlerun", name: "Shuttle Run", equip: ["space_to_run"], muscle: ["Full Body", "Quads"], pattern: "run", unit: "m", cardio: true, desc: "Sprint back and forth between two markers, touching the line each time.", cues: ["Low, quick turns", "Drive out of each turn", "Stay on the balls of the feet"] },

  // Cardio
  { id: "jumprope", name: "Jump Rope (Singles)", equip: ["jump_rope"], muscle: ["Full Body"], pattern: "jump", unit: "sec", cardio: true, desc: "Steady single-unders at a relaxed, sustainable pace.", cues: ["Small jumps, low to the ground", "Wrists do the turning", "Relaxed shoulders"], scale: { down: "Slow the pace or take short breaks.", up: "Increase the pace, or mix in double-unders." } },
  { id: "treadmillrun", name: "Treadmill Run", equip: ["cardio_machine"], muscle: ["Full Body"], pattern: "run", unit: "min", cardio: true, desc: "Steady or interval running on the treadmill.", cues: ["Relaxed shoulders", "Quick cadence", "Even breathing pattern"] },
  { id: "stairclimber", name: "Stair Climber", equip: ["cardio_machine"], muscle: ["Quads", "Glutes"], pattern: "run", unit: "min", cardio: true, desc: "Steady climbing pace, full foot on each step.", cues: ["Stand tall, avoid leaning on the rails", "Full foot contact", "Steady rhythm"] },
  { id: "elliptical", name: "Elliptical", equip: ["cardio_machine"], muscle: ["Full Body"], pattern: "run", unit: "min", cardio: true, desc: "Low-impact steady-state cardio.", cues: ["Even pressure through both legs", "Engage the arms on the handles", "Steady, sustainable pace"] },
  { id: "swimlaps", name: "Swimming (Laps)", equip: ["pool"], muscle: ["Full Body"], pattern: "run", unit: "min", cardio: true, desc: "Continuous lap swimming at an easy, sustainable pace.", cues: ["Long, relaxed strokes", "Steady breathing pattern", "Even pace lap to lap"] },
  { id: "briskwalk", name: "Brisk / Incline Walk", equip: ["bodyweight", "space_to_run", "cardio_machine"], muscle: ["Full Body", "Quads"], pattern: "run", unit: "min", cardio: true, desc: "A fast walk, flat or on an incline, that keeps the heart rate up without running.", cues: ["Quick, purposeful steps", "Swing the arms", "Stand tall through the torso"] },
  { id: "rowintervals", name: "Row Intervals", equip: ["rowing_machine"], muscle: ["Back", "Full Body"], pattern: "row", unit: "cal", cardio: true, desc: "Alternating hard pulls with easy recovery pace on the rower.", cues: ["Legs drive first, arms finish", "Hard pace on the work interval", "Easy, relaxed pace on the rest"] },
  { id: "bikeintervals", name: "Assault Bike Intervals", equip: ["assault_bike"], muscle: ["Full Body", "Quads"], pattern: "run", unit: "cal", cardio: true, desc: "Short, hard efforts on the bike with an easy pace between.", cues: ["Push and pull hard on the work interval", "Recover at an easy spin", "Consistent effort each round"] },
];
const EX_MAP = Object.fromEntries(EX.map((e) => [e.id, e]));
const MUSCLE_GROUPS = ["Chest", "Back", "Shoulders", "Arms", "Quads", "Hamstrings", "Glutes", "Core", "Full Body"];

const BENCHMARKS = [
  { name: "Fran", format: "RFT", rounds: 3, need: ["barbell", "pull_up_bar"], items: [
    { id: "thruster", reps: "21-15-9", load: "43/29kg", scale: { down: "Use an empty bar (20/15kg) or light dumbbells, and consider 15-12-9 if 21 is too many.", up: "Move up to 61/43kg, or add a 4th round for a 21-15-9-6 finisher." } },
    { id: "pullup", reps: "21-15-9", scale: { down: "Sub banded pull-ups or ring rows for the same rep scheme.", up: "Hold a chest-to-bar standard, or add a light weight vest." } },
  ] },
  { name: "Cindy", format: "AMRAP", timeCapMin: 20, need: ["pull_up_bar"], items: [
    { id: "pullup", reps: 5, scale: { down: "Sub ring rows or jumping pull-ups.", up: "Upgrade to strict or chest-to-bar pull-ups." } },
    { id: "pushup", reps: 10, scale: { down: "Do push-ups from the knees.", up: "Elevate the feet for a deficit push-up." } },
    { id: "airsquat", reps: 15, scale: { down: "Reduce to 10 squats per round if needed.", up: "Add a 1-second pause at the bottom of each rep." } },
  ] },
  { name: "Helen", format: "RFT", rounds: 3, need: ["kettlebell", "pull_up_bar", "space_to_run"], items: [
    { id: "run400", reps: "400m", scale: { down: "Reduce to a 200m run.", up: "Run 400m in a light weight vest." } },
    { id: "kbswing", reps: 21, load: "24/16kg", scale: { down: "Use a lighter kettlebell (16/12kg).", up: "Increase to 32/24kg." } },
    { id: "pullup", reps: 12, scale: { down: "Sub banded pull-ups for the same reps.", up: "Upgrade to chest-to-bar pull-ups." } },
  ] },
  { name: "Grace", format: "RFT", rounds: 1, need: ["barbell"], items: [
    { id: "cleanjerk", reps: 30, load: "61/43kg", scale: { down: "Reduce the weight significantly and break into small, steady sets.", up: "Move toward Rx+ weight (70/48kg) for an unbroken attempt." } },
  ] },
  { name: "Annie", format: "RFT", rounds: 5, need: ["jump_rope", "bodyweight"], items: [
    { id: "doubleunder", reps: "50-40-30-20-10", scale: { down: "Sub double the single-unders for each number in the scheme.", up: "String each round of double-unders unbroken." } },
    { id: "situp", reps: "50-40-30-20-10", scale: { down: "Reduce range of motion, or lower the total reps per round.", up: "Hold a weight plate overhead throughout each rep." } },
  ] },
];

const CHIPS = ["AMRAP", "EMOM", "RFT", "Tabata", "Chipper", "Strength", "Benchmark/Hero", "Upper Body", "Lower Body", "Core", "Full Body", "Kettlebell", "Bodyweight", "Gymnastic"];

/* ============================== YOGA LIBRARY ============================== */
const YOGA_AREAS = {
  "Wake Up": ["mountain", "catcow", "downdog", "forwardfold_pose", "cobra", "childspose_pose", "happybaby_pose"],
  Hips: ["pigeon_pose", "happybaby_pose", "lizard", "butterfly", "frog_pose", "onelegpigeon_pose", "sleepingpigeon_pose"],
  "Lower Back": ["catcow", "childspose_pose", "cobra", "seatedtwist_pose", "plow_pose", "halflord_pose"],
  Neck: ["neckroll", "eartoshoulder"],
  Shoulders: ["eagle_pose", "threadneedle_pose", "cobra", "cowface_pose", "reversewarrior_pose", "gate_pose"],
  Hamstrings: ["forwardfold_pose", "halfsplit", "downdog", "standingsplit_pose", "widelegfold_pose"],
  Splits: ["lizard", "halfsplit", "pigeon_pose", "standingsplit_pose", "gate_pose"],
  "Full Body Flow": ["mountain", "downdog", "warrior_pose", "forwardfold_pose", "childspose_pose", "puppy_pose", "dolphin_pose", "humblewarrior_pose"],
  "Standing Poses": ["mountain", "chair_pose", "triangle_pose", "tree_pose", "sidestretch_pose", "goddess_pose", "highlunge_pose", "starpose_pose", "widelegfold_pose"],
  Backbends: ["cobra", "camel_pose", "locust_pose", "fish_pose", "bow_pose", "sphinx_pose", "bridge_pose"],
  "Core & Balance": ["boat_pose", "crow_pose", "tree_pose", "standingsplit_pose"],
  Relaxation: ["corpse_pose", "legsupwall_pose", "reclinedtwist_pose", "easypose_pose", "childspose_pose"],
};
const POSE_LIB = {
  mountain: { name: "Mountain Pose", hold: 20, diagram: "mountain", desc: "Stand tall, feet grounded, reach through the crown." },
  catcow: { name: "Cat-Cow", hold: 30, diagram: "catcow", desc: "On all fours, alternate arching and rounding the spine." },
  downdog: { name: "Downward Dog", hold: 30, diagram: "downdog", desc: "Inverted V, hips high, heels reaching toward the floor." },
  forwardfold_pose: { name: "Forward Fold", hold: 30, diagram: "forwardfold", desc: "Hinge from the hips, let the head and neck relax." },
  cobra: { name: "Cobra", hold: 20, diagram: "cobra", desc: "Lying face down, press the chest up, shoulders down." },
  childspose_pose: { name: "Child's Pose", hold: 30, diagram: "childspose", desc: "Kneel and fold forward, arms extended, forehead down." },
  happybaby_pose: { name: "Happy Baby", hold: 30, diagram: "happybaby", desc: "On your back, grab the outside of your feet, knees toward armpits." },
  pigeon_pose: { name: "Pigeon", hold: 40, diagram: "pigeon", desc: "Front shin across the mat, back leg extended, fold forward." },
  lizard: { name: "Lizard Lunge", hold: 30, diagram: "warrior", desc: "Deep lunge, hands inside the front foot, sink the hips low." },
  butterfly: { name: "Butterfly", hold: 30, diagram: "pigeon", desc: "Soles of the feet together, gently press knees toward the floor." },
  seatedtwist_pose: { name: "Seated Twist", hold: 25, diagram: "seatedtwist", desc: "Sit tall, twist toward the back hand, lengthen the spine." },
  neckroll: { name: "Neck Roll", hold: 20, diagram: "mountain", desc: "Slow, gentle circles, both directions." },
  eartoshoulder: { name: "Ear to Shoulder", hold: 20, diagram: "mountain", desc: "Tilt the ear toward the shoulder, feel the opposite side stretch." },
  eagle_pose: { name: "Eagle Arms", hold: 25, diagram: "eagle", desc: "Wrap the arms, lift the elbows, feel the upper back open." },
  threadneedle_pose: { name: "Thread the Needle", hold: 30, diagram: "threadneedle", desc: "On all fours, thread one arm under the body, rest the shoulder down." },
  halfsplit: { name: "Half Split", hold: 30, diagram: "forwardfold", desc: "Front leg straight, hinge forward over the shin." },
  warrior_pose: { name: "Warrior II", hold: 25, diagram: "warrior", desc: "Wide stance lunge, arms extended front and back, gaze over the front hand." },
  tree_pose: { name: "Tree Pose", hold: 25, diagram: "tree", desc: "Balance on one foot, sole pressed to the inner thigh, hands overhead." },
  chair_pose: { name: "Chair Pose", hold: 25, diagram: "chair", desc: "Sink the hips back like sitting in a chair, arms reach overhead." },
  triangle_pose: { name: "Triangle Pose", hold: 30, diagram: "triangle", desc: "Wide stance, hinge sideways over the front leg, reach one arm up." },
  boat_pose: { name: "Boat Pose", hold: 20, diagram: "boat", desc: "Balance on the sit bones, legs and chest lifted into a V." },
  bridge_pose: { name: "Bridge Pose", hold: 25, diagram: "bridge", desc: "Lying on your back, press through the feet and lift the hips." },
  camel_pose: { name: "Camel Pose", hold: 20, diagram: "camel", desc: "Kneel and arch back, reaching for the heels." },
  locust_pose: { name: "Locust Pose", hold: 20, diagram: "locust", desc: "Lying face down, lift the chest and legs together." },
  fish_pose: { name: "Fish Pose", hold: 20, diagram: "fish", desc: "Lying on your back, arch the chest up, crown of the head light on the mat." },
  crow_pose: { name: "Crow Pose", hold: 15, diagram: "crow", desc: "Crouch forward, knees resting on the backs of the arms, lift the feet." },
  plow_pose: { name: "Plow Pose", hold: 25, diagram: "plow", desc: "Lying on your back, sweep the legs overhead toward the floor." },
  legsupwall_pose: { name: "Legs Up the Wall", hold: 60, diagram: "legsupwall", desc: "Lying on your back, legs resting straight up a wall." },
  reclinedtwist_pose: { name: "Reclined Twist", hold: 30, diagram: "reclinedtwist", desc: "Lying on your back, drop both knees gently to one side." },
  corpse_pose: { name: "Corpse Pose", hold: 60, diagram: "corpse", desc: "Lie flat, palms up, and let everything release." },
  easypose_pose: { name: "Easy Pose", hold: 30, diagram: "easypose", desc: "Sit cross-legged, hands resting on the knees, spine tall." },
  frog_pose: { name: "Frog Pose", hold: 30, diagram: "frog", desc: "Wide knees, forearms down, sink the hips back." },
  puppy_pose: { name: "Puppy Pose", hold: 30, diagram: "puppy", desc: "Hips stay over the knees, chest and arms melt toward the floor." },
  standingsplit_pose: { name: "Standing Split", hold: 25, diagram: "standingsplit", desc: "Fold forward and lift the back leg toward the ceiling." },
  reversewarrior_pose: { name: "Reverse Warrior", hold: 25, diagram: "reversewarrior", desc: "From Warrior II, arc the back arm down along the back leg." },
  gate_pose: { name: "Gate Pose", hold: 25, diagram: "gate", desc: "Kneeling, extend one leg out to the side and reach over it." },
  sidestretch_pose: { name: "Standing Side Stretch", hold: 20, diagram: "sidestretch", desc: "Reach one arm overhead and lean gently to the side." },
  widelegfold_pose: { name: "Wide-Legged Forward Fold", hold: 30, diagram: "widelegfold", desc: "Wide stance, hinge forward from the hips, let the head hang." },
  goddess_pose: { name: "Goddess Pose", hold: 25, diagram: "goddess", desc: "Wide squat, knees tracking over the toes, arms in a cactus shape." },
  highlunge_pose: { name: "Crescent Lunge", hold: 25, diagram: "highlunge", desc: "Deep lunge, back leg straight, arms reach overhead." },
  sphinx_pose: { name: "Sphinx Pose", hold: 25, diagram: "sphinx", desc: "Lying face down, prop up on the forearms, chest gently lifted." },
  cowface_pose: { name: "Cow Face Pose", hold: 25, diagram: "cowface", desc: "Seated, stack the knees, reach one arm up and one behind the back." },
  bow_pose: { name: "Bow Pose", hold: 20, diagram: "bow", desc: "Lying face down, hold the ankles and lift the chest and thighs." },
  dolphin_pose: { name: "Dolphin Pose", hold: 25, diagram: "downdog", desc: "Forearms down, hips lift into an inverted V." },
  sunsalute_fold_pose: { name: "Standing Forward Bend", hold: 20, diagram: "forwardfold", desc: "Fold from the hips, knees soft, hands toward the floor." },
  onelegpigeon_pose: { name: "One-Legged King Pigeon", hold: 35, diagram: "pigeon", desc: "Deepen pigeon and reach back for the lifted foot." },
  sleepingpigeon_pose: { name: "Sleeping Pigeon", hold: 40, diagram: "pigeon", desc: "Pigeon hips, then fold all the way forward over the front leg." },
  halflord_pose: { name: "Half Lord of the Fishes", hold: 25, diagram: "seatedtwist", desc: "Seated, cross one leg over and twist gently toward it." },
  starpose_pose: { name: "Star Pose", hold: 20, diagram: "goddess", desc: "Wide stance, arms and legs spread wide like a star." },
  humblewarrior_pose: { name: "Humble Warrior", hold: 25, diagram: "puppy", desc: "From a lunge, clasp the hands behind and fold forward between the knees." },
};

/* ============================== WARM-UP / COOL-DOWN ============================== */
const COOLDOWN = [
  { name: "Standing Quad Stretch", seconds: 30, pattern: "calf", desc: "Pull one heel toward the glute, knees together, hold, switch sides." },
  { name: "Standing Hamstring Stretch", seconds: 30, pattern: "hinge", desc: "Hinge forward over a slightly bent front leg, let the head hang." },
  { name: "Chest Doorway Stretch", seconds: 30, pattern: "press", desc: "Forearm on a wall or door frame, gently rotate away to open the chest." },
  { name: "Child's Pose", seconds: 30, pose: "childspose", desc: "Kneel and fold forward, arms extended, letting the low back release." },
  { name: "Cat-Cow", seconds: 30, pose: "catcow", desc: "On all fours, slowly alternate arching and rounding the spine." },
  { name: "Figure-Four Stretch", seconds: 30, pattern: "squat", desc: "Lying down, cross one ankle over the opposite knee and pull gently toward you." },
  { name: "Deep Breathing", seconds: 60, pattern: "plank", desc: "Slow nasal breathing, long exhales, to bring the heart rate down." },
];

/* ============================== 5K TRAINING ============================== */
function fmtMinSec(totalSeconds) {
  const m = Math.floor(totalSeconds / 60);
  const s = Math.round(totalSeconds % 60).toString().padStart(2, "0");
  return `${m}:${s}`;
}
const RACE_DISTANCES = [
  { id: "5k", label: "5K", km: 5, goals: [15, 17.5, 20, 22.5, 25, 27.5, 30, 35, 40] },
  { id: "10k", label: "10K", km: 10, goals: [32, 37, 42, 47, 52, 57, 62, 70, 80] },
  { id: "half", label: "Half", km: 21.0975, goals: [75, 85, 95, 105, 115, 125, 135, 150, 165] },
  { id: "full", label: "Marathon", km: 42.195, goals: [160, 180, 200, 220, 240, 260, 280, 310, 340] },
];
function paceRows(dist) {
  return dist.goals.map((min) => {
    const totalSec = min * 60;
    const perKm = totalSec / dist.km;
    return { goal: fmtMinSec(totalSec), perKm: fmtMinSec(perKm), perMile: fmtMinSec(perKm * 1.60934) };
  });
}

/* Progressive overload: read the last logged load for a lift and nudge it up.
   Upper-body lifts move in smaller jumps than lower-body ones. */
const SMALL_JUMP_PATTERNS = ["press", "pushup", "curl", "row", "pullup"];
function suggestNextLoad(history, exerciseName, pattern, units) {
  const last = history.find((h) => h.type === "lift" && h.name === exerciseName && h.loadKg);
  if (!last) return null;
  const stepKg = SMALL_JUMP_PATTERNS.includes(pattern) ? 2.5 : 5;
  const next = last.loadKg + stepKg;
  return { last: displayWeight(last.loadKg, units), next: displayWeight(next, units), nextKg: next };
}

const RUNNING_TIPS = [
  "Run 3-4 times a week, with at least one rest or cross-training day between harder efforts.",
  "Follow the 80/20 rule — about 80% of your running should feel easy and conversational.",
  "Include one interval or tempo session per week to build speed and lactate threshold.",
  "Increase weekly mileage by no more than about 10% from one week to the next.",
  "Warm up before every run and stretch after — use the Warm-Up and Cool-Down routines in Tools.",
  "Aim for a cadence around 170-180 steps per minute to run more efficiently — train it with the Pace Metronome.",
  "Practice your goal race pace during at least one workout per week in the final few weeks.",
  "Hydrate well and eat something familiar 1-2 hours before longer runs.",
  "Prioritize sleep — most of the adaptation from training happens during recovery.",
  "Taper your mileage the week of the race so you toe the line fresh, not fatigued.",
];
const FIVEK_PLAN = [
  { week: 1, mon: "Rest", tue: "Easy run 20 min", wed: "Intervals: 6x400m, 90s rest", thu: "Rest or cross-train", fri: "Easy run 20 min", sat: "Long run 30 min easy", sun: "Rest" },
  { week: 2, mon: "Rest", tue: "Easy run 25 min", wed: "Intervals: 8x400m, 90s rest", thu: "Rest or cross-train", fri: "Easy run 25 min", sat: "Long run 35 min easy", sun: "Rest" },
  { week: 3, mon: "Rest", tue: "Easy run 25 min", wed: "Tempo: 15 min at goal pace + 5 min easy", thu: "Rest or cross-train", fri: "Easy run 20 min", sat: "Long run 40 min easy", sun: "Rest" },
  { week: 4, mon: "Rest", tue: "Easy run 15 min", wed: "Short intervals: 4x400m at race pace", thu: "Rest", fri: "Easy 10 min shakeout", sat: "5K Race Day!", sun: "Rest" },
];

/* ============================== SHARED UI ============================== */
function TopBar({ title, onBack, right, accent = T.ember }) {
  return (
    <div className="px-4 pt-4 pb-3 sticky top-0 z-20"
      style={{ background: `${T.ink}EE`, backdropFilter: "blur(10px)" }}>
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          {onBack && (
            <button aria-label="Back" onClick={onBack} className="p-1 -ml-1 rounded-full" style={{ color: T.chalk }}>
              <ChevronLeft size={22} />
            </button>
          )}
          <h1 style={{ ...display, color: T.chalk, fontSize: 23, letterSpacing: "0.02em" }}>{title}</h1>
        </div>
        <div>{right}</div>
      </div>
      {/* Rule fades out to the right, like a chalk stroke running dry. */}
      <div className="mt-2.5 h-[2px] rounded-full"
        style={{ background: `linear-gradient(90deg, ${accent}, ${accent}00 62%)` }} />
    </div>
  );
}

function Chip({ active, onClick, children, accent = T.ember }) {
  return (
    <button onClick={onClick} className="px-3 py-1.5 rounded-full text-sm mr-2 mb-2 border whitespace-nowrap"
      style={{
        background: active ? `linear-gradient(160deg, ${accent}, ${accent}CC)` : T.surface,
        borderColor: active ? accent : T.line,
        color: active ? T.onAccent : T.chalkDim,
        fontWeight: active ? 700 : 500,
        boxShadow: active ? `0 2px 10px ${accent}44` : "none",
      }}>
      {children}
    </button>
  );
}

function EquipmentModal({ open, onClose, equipment, setEquipment }) {
  if (!open) return null;
  const toggle = (id) => setEquipment((prev) => ({ ...prev, [id]: !prev[id] }));
  const setAll = (val) => { const next = {}; EQUIPMENT.forEach((e) => (next[e.id] = val)); setEquipment(next); };
  return (
    <div className="fixed inset-0 z-50 flex items-end" style={{ background: "rgba(22,24,29,0.42)", backdropFilter: "blur(2px)" }}>
      <div className="w-full rounded-t-2xl p-5 max-h-[85vh] overflow-y-auto" style={{ background: T.surface }}>
        <div className="flex items-center justify-between mb-4">
          <h2 style={{ ...display, color: T.chalk, fontSize: 20 }}>Your Equipment</h2>
          <button aria-label="Close" onClick={onClose}><X color={T.muted} /></button>
        </div>
        <div className="flex gap-2 mb-4">
          <button onClick={() => setAll(true)} className="text-sm px-3 py-1 rounded-full" style={{ background: T.emberDim, color: T.emberInk }}>Select all</button>
          <button onClick={() => setAll(false)} className="text-sm px-3 py-1 rounded-full" style={{ background: T.surface2, color: T.muted }}>Clear all</button>
        </div>
        <div className="space-y-1">
          {EQUIPMENT.map((eq) => (
            <button key={eq.id} onClick={() => toggle(eq.id)} className="w-full flex items-center justify-between py-3 border-b" style={{ borderColor: T.line }}>
              <span className="flex items-center gap-3" style={{ color: T.chalk }}><span className="text-lg">{eq.icon}</span>{eq.name}</span>
              <span className="w-11 h-6 rounded-full relative transition-colors" style={{ background: equipment[eq.id] ? T.ember : T.line }}>
                <span className="absolute top-0.5 w-5 h-5 rounded-full transition-all" style={{ left: equipment[eq.id] ? 22 : 2, background: T.surface }} />
              </span>
            </button>
          ))}
        </div>
        <button onClick={onClose} className="w-full mt-5 py-3 rounded-xl" style={{ ...emberButton(), fontSize: 16 }}>Done</button>
      </div>
    </div>
  );
}

/* Shared one-shot tone. Used for interval transitions and rest-timer alerts so
   you can keep your eyes off the screen mid-set. */
/* ============================== SOUND ==============================
   Everything here is synthesised at runtime — no audio files, so nothing to
   license, nothing to download, and no app-size cost. It also means each cue
   can be shaped to its meaning rather than being a generic ping: rising for
   an inhale, falling for an exhale, a bright triad when you finish.

   Browsers block audio until the user has interacted with the page, so the
   context is created lazily on the first real tap and unlocked from there.
==================================================================== */
const Sound = {
  ctx: null,
  volume: 0.6,

  ready() {
    try {
      if (!this.ctx) {
        const Ctx = window.AudioContext || window.webkitAudioContext;
        if (!Ctx) return null;
        this.ctx = new Ctx();
      }
      if (this.ctx.state === "suspended") this.ctx.resume();
      return this.ctx;
    } catch (e) {
      return null;
    }
  },

  /* One shaped tone. `glide` bends the pitch over the note's life, which is
     what makes an inhale feel like it's opening up and an exhale like it's
     letting go. */
  tone({ freq = 880, to = null, ms = 140, type = "sine", gain = 1, delay = 0 }) {
    const ctx = this.ready();
    if (!ctx) return;
    const t0 = ctx.currentTime + delay;
    const osc = ctx.createOscillator();
    const amp = ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, t0);
    if (to) osc.frequency.exponentialRampToValueAtTime(Math.max(20, to), t0 + ms / 1000);

    // A short attack and a smooth tail — square-edged gain changes click.
    const peak = Math.max(0.0001, this.volume * gain * 0.3);
    amp.gain.setValueAtTime(0.0001, t0);
    amp.gain.exponentialRampToValueAtTime(peak, t0 + 0.012);
    amp.gain.exponentialRampToValueAtTime(0.0001, t0 + ms / 1000);

    osc.connect(amp);
    amp.connect(ctx.destination);
    osc.start(t0);
    osc.stop(t0 + ms / 1000 + 0.02);
  },

  /* ---- Timer cues ---- */
  tick()        { this.tone({ freq: 660, ms: 70, type: "triangle", gain: 0.55 }); },   // 3-2-1
  go()          { this.tone({ freq: 520, to: 1040, ms: 260, type: "triangle" }); },
  workStart()   { this.tone({ freq: 880, ms: 180, type: "square", gain: 0.8 }); },
  restStart()   { this.tone({ freq: 520, to: 390, ms: 240, type: "sine" }); },
  roundDone()   { this.tone({ freq: 780, ms: 90, type: "triangle" }); this.tone({ freq: 1040, ms: 130, delay: 0.09 }); },
  finish() {
    // A rising major triad — unmistakably "done" without being a fanfare.
    [523, 659, 784, 1047].forEach((f, i) =>
      this.tone({ freq: f, ms: i === 3 ? 520 : 190, delay: i * 0.13, type: "triangle" }));
  },

  /* ---- Breath cues. Pitch follows the lungs. ---- */
  inhale(sec = 4)  { this.tone({ freq: 330, to: 560, ms: Math.min(sec, 6) * 1000 * 0.9, type: "sine", gain: 0.5 }); },
  exhale(sec = 6)  { this.tone({ freq: 500, to: 260, ms: Math.min(sec, 10) * 1000 * 0.9, type: "sine", gain: 0.5 }); },
  hold()           { this.tone({ freq: 420, ms: 130, type: "sine", gain: 0.35 }); },
  cycleDone()      { this.tone({ freq: 620, ms: 90, type: "triangle", gain: 0.4 }); },

  /* ---- Spoken cues. Built into the browser, so still no files. ---- */
  say(text, enabled) {
    if (!enabled) return;
    try {
      const synth = window.speechSynthesis;
      if (!synth) return;
      synth.cancel();
      const u = new SpeechSynthesisUtterance(text);
      u.rate = 1.05;
      u.pitch = 1;
      u.volume = Math.min(1, this.volume + 0.2);
      synth.speak(u);
    } catch (e) { /* no speech synthesis available */ }
  },

  stopSpeech() {
    try { window.speechSynthesis?.cancel(); } catch (e) { /* ignore */ }
  },
};

/* Kept for older call sites; new code should use the named cues above. */
function playBeep(freq = 880, ms = 140) {
  Sound.tone({ freq, ms });
}

function useTimer(soundEnabled = true) {
  const soundRef = useRef(soundEnabled);
  useEffect(() => { soundRef.current = soundEnabled; }, [soundEnabled]);
  const [mode, setMode] = useState(null);
  const [running, setRunning] = useState(false);
  const [seconds, setSeconds] = useState(0);
  const [phase, setPhase] = useState("work");
  const [round, setRound] = useState(1);
  const cfgRef = useRef({});
  const tickRef = useRef(null);

  useEffect(() => {
    if (!running) return;
    tickRef.current = setInterval(() => {
      setSeconds((s) => {
        const cfg = cfgRef.current;
        if (mode === "stopwatch") return s + 1;
        if (mode === "countdown") {
          if (s <= 1) { setRunning(false); if (soundRef.current) Sound.finish(); return 0; }
          if (s <= 3 && soundRef.current) Sound.tick();
          return s - 1;
        }
        if (mode === "interval") {
          if (s <= 1) {
            setPhase((p) => {
              if (p === "work") {
                if (round >= cfg.rounds) { setRunning(false); if (soundRef.current) Sound.finish(); return "done"; }
                setRound((r) => r + 1);
                if (soundRef.current) Sound.restStart();
                return "rest";
              }
              if (soundRef.current) Sound.workStart();
              return "work";
            });
            return phase === "work" ? cfg.rest : cfg.work;
          }
          return s - 1;
        }
        return s;
      });
    }, 1000);
    return () => clearInterval(tickRef.current);
  }, [running, mode, phase, round]);

  const start = (m, cfg = {}) => {
    if (soundRef.current) Sound.go();
    cfgRef.current = cfg; setMode(m); setPhase("work"); setRound(1);
    if (m === "stopwatch") setSeconds(0);
    if (m === "countdown") setSeconds(cfg.total);
    if (m === "interval") setSeconds(cfg.work);
    setRunning(true);
  };
  const pause = () => setRunning(false);
  const resume = () => setRunning(true);
  const reset = () => { setRunning(false); setSeconds(0); setMode(null); setRound(1); setPhase("work"); };
  return { mode, running, seconds, phase, round, start, pause, resume, reset };
}

/* ============================== METRONOME ============================== */
function useMetronome(defaultBpm = 170) {
  const [bpm, setBpm] = useState(defaultBpm);
  const [running, setRunning] = useState(false);
  const [beat, setBeat] = useState(0);
  const audioCtxRef = useRef(null);
  const intervalRef = useRef(null);

  const click = () => {
    try {
      if (!audioCtxRef.current) {
        const Ctx = window.AudioContext || window.webkitAudioContext;
        audioCtxRef.current = new Ctx();
      }
      const ctx = audioCtxRef.current;
      if (ctx.state === "suspended") ctx.resume();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.frequency.value = 1000;
      gain.gain.setValueAtTime(0.22, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.06);
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start();
      osc.stop(ctx.currentTime + 0.06);
    } catch (e) { /* audio not available, visual pulse still works */ }
  };

  const tick = () => { click(); setBeat((b) => b + 1); };

  const start = () => {
    setRunning(true);
    tick();
    intervalRef.current = setInterval(tick, 60000 / bpm);
  };
  const stop = () => { setRunning(false); clearInterval(intervalRef.current); };

  useEffect(() => {
    if (running) {
      clearInterval(intervalRef.current);
      intervalRef.current = setInterval(tick, 60000 / bpm);
    }
    // eslint-disable-next-line
  }, [bpm]);

  useEffect(() => () => clearInterval(intervalRef.current), []);

  return { bpm, setBpm, running, beat, start, stop };
}

function Metronome({ accent = T.ember }) {
  const met = useMetronome(170);
  const presets = [
    { label: "Easy jog", bpm: 160 },
    { label: "Standard", bpm: 170 },
    { label: "Race pace", bpm: 180 },
  ];
  return (
    <div className="rounded-2xl p-5 text-center" style={cardStyle()}>
      <p style={{ ...display, color: T.muted, fontSize: 13 }}>Pace Metronome</p>
      <div className="flex justify-center my-4">
        <div key={met.beat} style={{ width: 70, height: 70, borderRadius: "9999px", background: accent, animation: "beatPulse 0.28s ease-out" }} />
      </div>
      <style>{`@keyframes beatPulse{0%{transform:scale(1.4);opacity:1}100%{transform:scale(1);opacity:0.75}}`}</style>
      <p style={{ ...numeric, color: T.chalk, fontSize: 42, fontWeight: 600 }}>{met.bpm}</p>
      <p className="text-xs mb-4" style={{ color: T.muted }}>steps per minute</p>
      <div className="flex items-center justify-center gap-3 mb-4">
        <button onClick={() => met.setBpm((b) => Math.max(120, b - 2))} className="w-10 h-10 rounded-full" style={{ background: T.surface2, color: T.chalk }}>-</button>
        <input type="range" min="120" max="200" value={met.bpm} onChange={(e) => met.setBpm(Number(e.target.value))} className="w-40" />
        <button onClick={() => met.setBpm((b) => Math.min(200, b + 2))} className="w-10 h-10 rounded-full" style={{ background: T.surface2, color: T.chalk }}>+</button>
      </div>
      <div className="flex justify-center gap-2 mb-5">
        {presets.map((p) => (
          <button key={p.bpm} onClick={() => met.setBpm(p.bpm)} className="text-xs px-3 py-1.5 rounded-full" style={{ background: met.bpm === p.bpm ? accent : T.surface2, color: met.bpm === p.bpm ? T.ink : T.muted }}>
            {p.label} · {p.bpm}
          </button>
        ))}
      </div>
      {met.running ? (
        <button aria-label="Pause" onClick={met.stop} className="w-full py-3 rounded-xl flex items-center justify-center gap-2" style={{ background: T.surface2, color: T.chalk, ...display, fontSize: 16 }}>
          <Pause size={18} /> Stop
        </button>
      ) : (
        <button onClick={met.start} className="w-full py-3 rounded-xl flex items-center justify-center gap-2" style={{ ...emberButton(accent), fontSize: 16 }}>
          <Volume2 size={18} /> Start
        </button>
      )}
    </div>
  );
}

function fmt(s) {
  const m = Math.floor(s / 60).toString().padStart(2, "0");
  const sec = Math.floor(s % 60).toString().padStart(2, "0");
  return `${m}:${sec}`;
}

function TimerOverlay({ timer, title, onClose, targetRounds }) {
  if (!timer.mode) return null;
  const isRest = timer.phase === "rest";
  const accent = timer.mode === "interval" && isRest ? T.sage : T.ember;
  return (
    <div className="fixed inset-0 z-50 flex flex-col items-center justify-center" style={{ background: RUNNER.bg }}>
      <button onClick={() => { timer.reset(); onClose(); }} className="absolute top-6 right-6" style={{ color: RUNNER.dim }}><X size={26} /></button>
      <p style={{ ...display, color: RUNNER.dim, fontSize: 14 }} className="mb-2">{title}</p>
      {timer.mode === "interval" && (
        <p style={{ ...display, color: accent, fontSize: 20 }} className="mb-2">
          {timer.phase === "done" ? "Done" : isRest ? "Rest" : "Work"} · Round {Math.min(timer.round, targetRounds || timer.round)}/{targetRounds || timer.round}
        </p>
      )}
      <p style={{ ...numeric, color: RUNNER.text, fontSize: 76, fontWeight: 600 }}>{fmt(timer.seconds)}</p>
      <div className="flex gap-4 mt-8">
        {timer.running ? <button aria-label="Pause" onClick={timer.pause} className="p-4 rounded-full" style={{ background: RUNNER.panel }}><Pause color={RUNNER.text} /></button>
          : <button aria-label="Play" onClick={timer.resume} className="p-4 rounded-full" style={{ background: accent }}><Play color={T.onAccent} /></button>}
        <button onClick={() => { timer.reset(); onClose(); }} className="p-4 rounded-full" style={{ background: RUNNER.panel }}><RotateCcw color={RUNNER.text} /></button>
      </div>
    </div>
  );
}

/* ============================== HOME ============================== */
function HomeScreen({ setTab, store, openEquipment, openSettings, openBuilder, onJustStart, onOpenMovement, onOpenYoga, onTalk }) {
  const { equipment, history, plan, settings, metrics, prs } = store;
  const equipCount = Object.values(equipment).filter(Boolean).length;
  const todayIdx = (new Date().getDay() + 6) % 7;
  const todayPlan = plan ? plan.days[todayIdx] : null;
  const today = new Date().toISOString().slice(0, 10);
  const checkedInToday = (metrics || []).some((m) => m.day === today);
  const loggedToday = history.some((h) => h.date.slice(0, 10) === today);

  const streak = useMemo(() => {
    if (!history.length) return 0;
    const days = new Set(history.map((h) => h.date.slice(0, 10)));
    let n = 0;
    const d = new Date();
    if (!days.has(d.toISOString().slice(0, 10))) d.setDate(d.getDate() - 1);
    while (days.has(d.toISOString().slice(0, 10))) { n++; d.setDate(d.getDate() - 1); }
    return n;
  }, [history]);

  const last7 = history.filter((h) => (Date.now() - new Date(h.date)) / 86400000 <= 7).length;
  const planned = plan ? plan.days.filter((d) => d !== "Rest").length : 0;
  const lastSession = history[0];

  const hour = new Date().getHours();
  const greeting = hour < 12 ? "Morning" : hour < 18 ? "Afternoon" : "Evening";

  const picks = useMemo(() => dailyPicks(store), [store.history, store.favorites, store.equipment]);

  /* The coach's read, computed locally. Showing the reasoning before the
     recommendation is what makes it feel like a coach rather than a shuffle
     button — you can see why it's saying what it's saying. */
  const coachRead = useMemo(() => {
    const recent = history.slice(0, 8);
    const hard = recent.filter((h) => h.rpe && h.rpe >= 8).length;
    const sore = (metrics || [])[0]?.soreness >= 4;
    const noteFlag = recent.find((h) => h.notes && /tight|sore|hurt|ache|pain|stiff/i.test(h.notes));
    const daysOff = history.length ? Math.floor((Date.now() - new Date(history[0].date)) / 86400000) : null;

    if (!history.length) return { tone: "new", text: "Log your first session and I'll start shaping what comes next." };
    if (noteFlag) return { tone: "care", text: `You mentioned something feeling off after ${noteFlag.name}. I'd work around it today.` };
    if (sore) return { tone: "care", text: "You checked in sore this morning. Today's a good day to move, not to load." };
    if (hard >= 3) return { tone: "care", text: `${hard} hard sessions in your last few. Worth taking the intensity down a notch.` };
    if (daysOff !== null && daysOff >= 4) return { tone: "nudge", text: `It's been ${daysOff} days. Let's start easy and rebuild the rhythm.` };
    if (streak >= 3) return { tone: "good", text: `${streak} days running. You're in a groove — keep the effort honest.` };
    return { tone: "good", text: "You're on track. Ready when you are." };
  }, [history, metrics, streak]);

  const toneColor = { care: T.amber, nudge: T.cyan, good: T.sage, new: T.violet }[coachRead.tone];

  return (
    <div className="pb-32" style={{ background: T.ink }}>
      {/* Greeting */}
      <div className="px-5 pt-7 pb-4 flex items-start justify-between">
        <div>
          <p style={{ ...display, color: T.muted, fontSize: 11, letterSpacing: "0.16em" }}>
            {new Date().toLocaleDateString(undefined, { weekday: "long", day: "numeric", month: "short" }).toUpperCase()}
          </p>
          <h1 style={{ color: T.chalk, fontSize: 27, fontWeight: 800, lineHeight: 1.1, marginTop: 2 }}>
            {greeting}
          </h1>
        </div>
        <div className="flex gap-2">
          <button aria-label="Equipment" onClick={openEquipment} className="flex items-center gap-1 px-3 py-2 rounded-full" style={{ background: T.surface, boxShadow: "0 2px 8px rgba(22,24,29,0.06)" }}>
            <Settings2 size={14} color={T.chalkDim} />
            <span style={{ ...numeric, color: T.chalkDim, fontSize: 12 }}>{equipCount}</span>
          </button>
          <button aria-label="Settings" onClick={openSettings} className="px-3 py-2 rounded-full" style={{ background: T.surface, boxShadow: "0 2px 8px rgba(22,24,29,0.06)" }}>
            <Wrench size={14} color={T.chalkDim} />
          </button>
        </div>
      </div>

      {/* COACH LEADS — its read comes first, then what it suggests. */}
      <button onClick={() => setTab("coach")} className="mx-4 w-[calc(100%-2rem)] text-left rounded-[22px] p-4 mb-3"
        style={{ ...cardStyle(), background: `linear-gradient(160deg, ${T.violetDim}, ${T.surface} 68%)`, border: `1px solid ${T.violet}22` }}>
        <div className="flex items-center gap-2.5 mb-2.5">
          <span className="w-8 h-8 rounded-full shrink-0" style={{
            background: "radial-gradient(circle at 34% 30%, #E5C8FF, #9B6BD6 58%, #6B45A0)",
            boxShadow: `0 0 14px ${T.violet}55`,
          }} />
          <div className="flex-1">
            <p style={{ color: T.chalk, fontWeight: 700, fontSize: 14 }}>Your coach</p>
            <p className="text-[11px]" style={{ color: T.muted }}>
              {history.length ? `Read your last ${Math.min(history.length, 12)} sessions` : "Ready to learn how you train"}
            </p>
          </div>
          <span className="w-2 h-2 rounded-full" style={{ background: toneColor }} />
        </div>
        <p className="text-sm" style={{ color: T.chalkDim, lineHeight: 1.5 }}>{coachRead.text}</p>
        <div className="flex gap-1.5 mt-3 flex-wrap">
          {["What should I do today?", "Why?"].map((c) => (
            <span key={c} className="px-2.5 py-1.5 rounded-full text-[11px]"
              style={{ background: T.surface, color: T.violet, border: `1px solid ${T.violet}33` }}>{c}</span>
          ))}
        </div>
      </button>

      {/* Say what you want. One box, typed or spoken, and a workout comes back. */}
      <button onClick={() => onTalk?.()} className="mx-4 w-[calc(100%-2rem)] text-left rounded-[22px] p-4 mb-3 flex items-center gap-3"
        style={{ ...cardStyle(), border: `1px solid ${T.ember}22` }}>
        <span className="w-11 h-11 rounded-full flex items-center justify-center shrink-0" style={{ background: T.emberDim }}>
          <Mic size={20} color={T.emberInk} />
        </span>
        <span className="flex-1">
          <span className="block" style={{ color: T.chalk, fontWeight: 700, fontSize: 14 }}>Tell me what you want</span>
          <span className="block text-[12px]" style={{ color: T.muted, lineHeight: 1.4 }}>
            Type or say it — "20 minutes, legs, no jumping" — and I'll build it.
          </span>
        </span>
        <ChevronRight size={16} color={T.muted} />
      </button>

      {/* Today's session. Still here, still one tap — but it sits after the
          coach's read rather than being the first thing you meet. */}
      <div className="mx-4 w-[calc(100%-2rem)] rounded-[22px] p-5 mb-3" style={cardStyle()}>
        <div className="flex items-center justify-between mb-1">
          <span style={{ ...display, color: T.ember, fontSize: 10, letterSpacing: "0.18em" }}>TODAY'S SESSION</span>
          {loggedToday && (
            <span className="flex items-center gap-1 px-2 py-0.5 rounded-full" style={{ background: T.sageDim }}>
              <Check size={10} color={T.sage} />
              <span style={{ ...display, color: T.sageInk, fontSize: 9 }}>Done</span>
            </span>
          )}
        </div>
        <p style={{ color: T.chalk, fontSize: 24, fontWeight: 800, lineHeight: 1.15 }}>
          {todayPlan || "Nothing scheduled"}
        </p>
        <p className="text-[12.5px] mt-1" style={{ color: T.muted }}>
          {todayPlan ? (loggedToday ? "Logged — nice work." : "Whenever you're ready.") : "No plan set — pick something below, or let me choose."}
        </p>

        <button onClick={onJustStart} className="w-full py-4 rounded-full mt-4" style={{ ...inkButton(), fontSize: 16 }}>
          Just start
        </button>
        <div className="flex gap-2 mt-2">
          <button onClick={() => setTab("generator")} className="flex-1 py-2.5 rounded-full text-[13px]"
            style={{ background: T.surface2, color: T.chalkDim, fontWeight: 600 }}>
            Choose a workout
          </button>
          <button aria-label="Build your own workout" onClick={openBuilder} className="px-4 rounded-full flex items-center justify-center"
            style={{ background: T.surface2, color: T.chalkDim }}>
            <Plus size={18} />
          </button>
        </div>
      </div>

      {/* Ring stats */}
      <div className="px-4 mb-1">
        <div className="grid grid-cols-3 gap-2.5">
          {[
            [streak, "DAY STREAK", streak > 0 ? T.ember : T.muted],
            [planned ? `${last7}/${planned}` : last7, "THIS WEEK", T.chalk],
            [Object.keys(prs).length, "PERSONAL BESTS", T.amber],
          ].map(([v, k, c]) => (
            <div key={k} className="rounded-[18px] py-3.5" style={cardStyle()}>
              <Stat value={v} label={k} color={c} size={23} />
            </div>
          ))}
        </div>
      </div>

      {/* Three things to try today — a movement, a flow, a breath. Seeded on
          the date so they hold steady all day rather than rerolling. */}
      <div className="px-4 mt-5">
        <SectionLabel>TRY SOMETHING NEW TODAY</SectionLabel>
        <div className="space-y-2.5">
          {picks.move && (
            <button onClick={() => onOpenMovement(picks.move)} className="w-full flex items-center gap-3.5 p-3.5 rounded-[18px]" style={cardStyle()}>
              <span className="w-10 h-10 rounded-[12px] flex items-center justify-center shrink-0" style={{ background: T.emberDim }}>
                <IconBadge pattern={picks.move.pattern} color={T.ember} size={26} still />
              </span>
              <span className="flex-1 text-left min-w-0">
                <span className="block text-[10px]" style={{ ...display, color: T.emberInk, letterSpacing: "0.12em" }}>NEW MOVEMENT</span>
                <span className="block truncate" style={{ color: T.chalk, fontWeight: 700, fontSize: 14.5 }}>{picks.move.name}</span>
                <span className="block text-[11px]" style={{ color: T.muted }}>{picks.move.muscle.slice(0, 2).join(" · ")}</span>
              </span>
              <ChevronRight size={15} color={T.muted} />
            </button>
          )}

          {picks.area && (
            <button onClick={() => onOpenYoga(picks.area)} className="w-full flex items-center gap-3.5 p-3.5 rounded-[18px]" style={cardStyle()}>
              <span className="w-10 h-10 rounded-[12px] flex items-center justify-center shrink-0" style={{ background: T.sageDim }}>
                <Wind size={18} color={T.sage} />
              </span>
              <span className="flex-1 text-left min-w-0">
                <span className="block text-[10px]" style={{ ...display, color: T.sageInk, letterSpacing: "0.12em" }}>YOGA FLOW</span>
                <span className="block truncate" style={{ color: T.chalk, fontWeight: 700, fontSize: 14.5 }}>{picks.area}</span>
                <span className="block text-[11px]" style={{ color: T.muted }}>
                  {YOGA_AREAS[picks.area].length} poses · about {Math.max(1, Math.round(YOGA_AREAS[picks.area].reduce((n, k) => n + POSE_LIB[k].hold, 0) / 60))} min
                </span>
              </span>
              <ChevronRight size={15} color={T.muted} />
            </button>
          )}

          {picks.breath && (
            <button onClick={() => setTab("breath")} className="w-full flex items-center gap-3.5 p-3.5 rounded-[18px]" style={cardStyle()}>
              <span className="w-10 h-10 rounded-[12px] flex items-center justify-center shrink-0" style={{ background: T.breathDim }}>
                <Wind size={18} color={T.breath} />
              </span>
              <span className="flex-1 text-left min-w-0">
                <span className="block text-[10px]" style={{ ...display, color: T.breathInk, letterSpacing: "0.12em" }}>BREATHING</span>
                <span className="block truncate" style={{ color: T.chalk, fontWeight: 700, fontSize: 14.5 }}>{picks.breath.name}</span>
                <span className="block text-[11px]" style={{ color: T.muted }}>{picks.breath.tag}</span>
              </span>
              <ChevronRight size={15} color={T.muted} />
            </button>
          )}
        </div>
      </div>

      {/* Explore — a bento grid rather than an even 2x2. Tile size carries the
          hierarchy, so Workouts reads as the main thing without needing a
          louder colour than everything around it. */}
      <div className="px-4 mt-5">
        <SectionLabel>EXPLORE</SectionLabel>
        <div className="grid grid-cols-3 gap-2.5">
          {/* Wide primary: the thing most people open */}
          <button onClick={() => setTab("generator")} className="col-span-2 rounded-[20px] p-4 text-left relative overflow-hidden"
            style={cardStyle()}>
            <span className="absolute right-0 top-0 bottom-0 w-1"
              style={{ background: `linear-gradient(${T.ember}, ${T.ember}00)` }} />
            <span className="w-11 h-11 rounded-[13px] flex items-center justify-center mb-3" style={{ background: T.emberDim }}>
              <Dumbbell size={20} color={T.ember} />
            </span>
            <p style={{ color: T.chalk, fontWeight: 800, fontSize: 17 }}>Workouts</p>
            <p className="text-[11.5px] mt-0.5" style={{ color: T.muted }}>
              {EX.length} movements · every level
            </p>
          </button>

          {/* Tall: cardio runs the full height beside it */}
          <button onClick={() => setTab("cardio")} className="row-span-2 rounded-[20px] p-3.5 text-left flex flex-col justify-between"
            style={cardStyle()}>
            <span className="w-10 h-10 rounded-[12px] flex items-center justify-center" style={{ background: T.cyanDim }}>
              <HeartPulse size={18} color={T.cyan} />
            </span>
            <span>
              <p style={{ color: T.chalk, fontWeight: 800, fontSize: 14.5, lineHeight: 1.15 }}>Cardio</p>
              <p className="text-[10.5px] mt-1" style={{ color: T.muted }}>Pace, metronome &amp; race plans</p>
            </span>
          </button>

          {/* Two halves tucked under the wide tile */}
          <button onClick={() => setTab("yoga")} className="rounded-[20px] p-3.5 text-left" style={cardStyle()}>
            <span className="w-9 h-9 rounded-[11px] flex items-center justify-center" style={{ background: T.sageDim }}>
              <Wind size={16} color={T.sage} />
            </span>
            <p style={{ color: T.chalk, fontWeight: 700, fontSize: 13.5 }} className="mt-2">Yoga</p>
            <p className="text-[10.5px]" style={{ color: T.muted }}>{Object.keys(YOGA_AREAS).length} flows</p>
          </button>

          <button onClick={() => setTab("breath")} className="rounded-[20px] p-3.5 text-left" style={cardStyle()}>
            <span className="w-9 h-9 rounded-[11px] flex items-center justify-center" style={{ background: T.breathDim }}>
              <Wind size={16} color={T.breath} />
            </span>
            <p style={{ color: T.chalk, fontWeight: 700, fontSize: 13.5 }} className="mt-2">Breath</p>
            <p className="text-[10.5px]" style={{ color: T.muted }}>{BREATH_PATTERNS.length} patterns</p>
          </button>
        </div>
      </div>

      {/* Recent */}
      <div className="px-4 mt-5">
        <SectionLabel>RECENT</SectionLabel>
        {lastSession ? (
          <button onClick={() => setTab("progress")} className="w-full flex items-center gap-3 p-3.5 rounded-[18px] mb-2.5" style={cardStyle()}>
            <span className="w-9 h-9 rounded-[11px] flex items-center justify-center shrink-0" style={{ background: T.amberDim }}>
              <Flame size={16} color={T.amber} />
            </span>
            <span className="flex-1 text-left min-w-0">
              <span className="block truncate" style={{ color: T.chalk, fontWeight: 700, fontSize: 14 }}>{lastSession.name}</span>
              <span className="block text-[11px]" style={{ color: T.muted }}>
                {new Date(lastSession.date).toLocaleDateString(undefined, { weekday: "short" })}
                {lastSession.result ? ` · ${lastSession.result}` : ""}
              </span>
            </span>
            {lastSession.rpe && <LoadRing rpe={lastSession.rpe} size={32} />}
            <ChevronRight size={15} color={T.muted} />
          </button>
        ) : (
          <div className="p-4 rounded-[18px] mb-2.5 text-center" style={cardStyle()}>
            <p className="text-sm" style={{ color: T.muted }}>No sessions yet — your first one shows up here.</p>
          </div>
        )}

        <div className="grid grid-cols-4 gap-2.5">
          {[
            { id: "plan", label: "Plan", icon: CalendarDays },
            { id: "progress", label: "Progress", icon: TrendingUp },
            { id: "favorites", label: "Saved", icon: Star },
            { id: "tools", label: "Tools", icon: Wrench },
          ].map((c) => {
            const Icon = c.icon;
            return (
              <button key={c.id} onClick={() => setTab(c.id)} className="rounded-[16px] p-3 flex flex-col items-center gap-1.5" style={cardStyle()}>
                <Icon size={16} color={T.chalkDim} />
                <span className="text-[10px]" style={{ color: T.chalkDim }}>{c.label}</span>
              </button>
            );
          })}
        </div>

        {!checkedInToday && (
          <p className="text-[11px] text-center mt-4" style={{ color: T.muted }}>
            No check-in today — Progress → Check in
          </p>
        )}
      </div>
    </div>
  );
}

/* ============================== GENERATOR ============================== */
function pick(arr, n) {
  const copy = [...arr]; const out = [];
  while (copy.length && out.length < n) out.push(copy.splice(Math.floor(Math.random() * copy.length), 1)[0]);
  return out;
}

function GeneratorScreen({ store, openEquipment, openBuilder, injectedWod, clearInjected, onLog, autoGenerate, clearAutoGenerate }) {
  const { equipment, settings, savedWods, toggleSavedWod } = store;
  const [chips, setChips] = useState([]);
  const [wod, setWod] = useState(null);
  const [tempo, setTempo] = useState("none");
  const [showLoads, setShowLoads] = useState(false);
  const [intensity, setIntensity] = useState(1.0);   // multiplier on the rep-based %
  const [tempoOpen, setTempoOpen] = useState(false);
  const [difficulty, setDifficulty] = useState(null);   // null = roll it
  const [duration, setDuration] = useState(null);       // null = roll it
  const recentSigs = useRef([]);
  const [dialOpen, setDialOpen] = useState(false);
  const [explain, setExplain] = useState(null);
  // A WOD handed over from the builder should land here ready to run.
  useEffect(() => {
    if (injectedWod) { setWod(injectedWod); clearInjected(); }
  }, [injectedWod, clearInjected]);

  // Arriving from "Just start" should produce a workout immediately — the
  // whole point is that no decisions were required to get here.
  useEffect(() => {
    if (autoGenerate) { generate(); clearAutoGenerate?.(); }
    // eslint-disable-next-line
  }, [autoGenerate]);
  const [runnerOpen, setRunnerOpen] = useState(false);

  const availEquip = useMemo(() => new Set(Object.keys(equipment).filter((k) => equipment[k])), [equipment]);
  const toggleChip = (c) => setChips((prev) => (prev.includes(c) ? prev.filter((x) => x !== c) : [...prev, c]));

  const bodyFocus = chips.find((c) => ["Upper Body", "Lower Body", "Core", "Full Body"].includes(c));
  const BODY_MAP = { "Upper Body": ["Chest", "Back", "Shoulders", "Arms"], "Lower Body": ["Quads", "Hamstrings", "Glutes"], Core: ["Core"], "Full Body": MUSCLE_GROUPS };

  function filteredPool() {
    return EX.filter((e) => {
      const equipOk = e.equip.some((eq) => eq === "bodyweight" || availEquip.has(eq));
      if (!equipOk) return false;
      if (chips.includes("Kettlebell") && !e.equip.includes("kettlebell")) return false;
      if (chips.includes("Bodyweight") && !e.equip.includes("bodyweight")) return false;
      if (chips.includes("Gymnastic") && !["pullup", "toestobar", "hspu", "ringdip"].includes(e.id)) return false;
      if (bodyFocus) { const groups = BODY_MAP[bodyFocus]; if (!e.muscle.some((m) => groups.includes(m))) return false; }
      return true;
    });
  }

  function toMove(m, reps) { return { name: m.name, reps, load: m.load, pattern: m.pattern, prop: m.prop, scale: m.scale }; }

  // Stamp the session-level choices onto whatever gets generated.
  const withSession = (w) => ({ ...w, tempo, showLoads, intensity });

  function buildWod() {
    // Anything you haven't chosen gets rolled. That's the point of a generator —
    // it should still hand you a complete workout from a blank slate.
    const rolled = [];
    const chosenFormat = chips.find((c) => ["AMRAP", "EMOM", "RFT", "Tabata", "Chipper", "Strength", "Benchmark/Hero"].includes(c));
    const format = chosenFormat || (() => {
      const opts = ["AMRAP", "EMOM", "RFT", "Tabata", "Chipper", "Strength", "Benchmark/Hero"];
      const f = opts[Math.floor(Math.random() * opts.length)];
      rolled.push(f);
      return f;
    })();

    const level = difficulty || (() => {
      // Weighted toward the middle: mostly moderate/hard, brutal is a rarity.
      const bag = ["easy", "moderate", "moderate", "hard", "hard", "brutal"];
      const d = bag[Math.floor(Math.random() * bag.length)];
      rolled.push(DIFFICULTY[d].name);
      return d;
    })();
    const D = DIFFICULTY[level];

    // An explicit minute target beats a vague "short" multiplier — people ask
    // "how long have I got", not "how short would you like this".
    const target = duration || (() => {
      const t = [5, 10, 15, 20, 30][Math.floor(Math.random() * 5)];
      rolled.push(`${t} min`);
      return t;
    })();

    const meta = () => ({ difficulty: D.name, level, rolled, target });
    // Land close to the target, with a touch of variance so two sessions at the
    // same setting aren't identical in length.
    const T_ = () => Math.max(4, Math.round(target * (0.92 + Math.random() * 0.16)));

    if (format === "Benchmark/Hero") {
      const candidates = BENCHMARKS.filter((b) => b.need.every((n) => n === "bodyweight" || availEquip.has(n)));
      // Don't hand back the benchmark that's already on screen if others exist.
      const fresh = candidates.filter((b) => b.name !== wod?.title);
      const fromList = fresh.length ? fresh : candidates;
      const bench = fromList[Math.floor(Math.random() * fromList.length)] || BENCHMARKS[1];
      return withSession({
        title: bench.name, format: bench.format, ...meta(),
        // Benchmarks are fixed by definition — scaling them makes them something else.
        difficulty: "As prescribed",
        note: "Benchmark workouts are run as written. Use the scaling notes if you need them.",
        meta: bench.rounds ? `${bench.rounds} rounds` : bench.timeCapMin ? `${bench.timeCapMin} min AMRAP` : "",
        movements: bench.items.map((it) => ({ name: EX_MAP[it.id].name, reps: it.reps, load: it.load, pattern: EX_MAP[it.id].pattern, prop: EX_MAP[it.id].prop, scale: it.scale || EX_MAP[it.id].scale })),
      });
    }

    // Easy sessions drop the high-skill movements entirely.
    let pool = filteredPool();
    if (!D.allowTechnical) {
      const simple = pool.filter((e) => !TECHNICAL.has(e.id));
      if (simple.length >= 4) pool = simple;
    }
    if (!pool.length) pool = filteredPool();

    const nMoves = (base) => Math.max(2, Math.min(pool.length, base + D.moves));

    if (format === "Strength") {
      const lifts = pool.filter((e) => ["squat", "hinge", "press", "row"].includes(e.pattern) && e.equip.includes("barbell"));
      const lift = (lifts.length ? lifts : pool)[Math.floor(Math.random() * (lifts.length ? lifts.length : pool.length))];
      const scheme = { easy: ["3 sets of 8", "8"], moderate: ["5 sets of 5", "5"], hard: ["5 sets of 3", "3"], brutal: ["7 sets of 2", "2"] }[level];
      return withSession({
        title: `Strength — ${lift.name}`, format: "Sets x Reps", ...meta(),
        meta: `${scheme[0]}, rest 2-3 min`,
        movements: [{ name: lift.name, reps: scheme[1], load: level === "easy" ? "moderate and clean" : "build to a heavy set", pattern: lift.pattern, prop: lift.prop, scale: lift.scale }],
      });
    }

    if (format === "Tabata") {
      const chosen = pick(pool, nMoves(2));
      return withSession({
        title: "Tabata", format: "Tabata", ...meta(),
        meta: `20s work / 10s rest × 8 rounds, per movement`,
        movements: chosen.map((m) => toMove(m, "max effort")),
        timerCfg: { work: 20, rest: 10, rounds: 8 * chosen.length },
      });
    }

    if (format === "EMOM") {
      const chosen = pick(pool, nMoves(3));
      const minutes = T_();
      return withSession({
        title: "EMOM", format: "EMOM", ...meta(),
        meta: `Every minute on the minute, ${minutes} minutes, rotating movements`,
        movements: chosen.map((m) => toMove(m,
          m.unit === "reps" ? `${scaleReps(jitter(10), level)}` :
          m.unit === "cal" ? `${scaleReps(jitter(12), level)} cal` : `${scaleReps(jitter(35), level)}s`)),
        timerCfg: { total: minutes * 60 },
      });
    }

    if (format === "RFT") {
      const chosen = pick(pool, nMoves(3));
      const rounds = { easy: 3, moderate: 4, hard: 5, brutal: 6 }[level] + (Math.random() < 0.35 ? 1 : 0);
      return withSession({
        title: "For Time", format: "RFT", ...meta(),
        meta: `${rounds} rounds for time`,
        movements: chosen.map((m) => toMove(m,
          m.unit === "reps" ? `${scaleReps(jitter(12), level)}` :
          m.unit === "cal" ? `${scaleReps(jitter(15), level)} cal` : `${scaleDistance(jitter(200), level)}m`)),
      });
    }

    if (format === "Chipper") {
      const chosen = pick(pool, nMoves(7));
      return withSession({
        title: "Chipper", format: "Chipper", ...meta(),
        meta: "One round, for time — work top to bottom",
        movements: chosen.map((m) => toMove(m,
          m.unit === "reps" ? `${scaleReps(jitter(15), level)}` :
          m.unit === "cal" ? `${scaleReps(jitter(20), level)} cal` : `${scaleDistance(jitter(200), level)}m`)),
      });
    }

    const chosen = pick(pool, nMoves(4));
    const timeCap = T_();
    return withSession({
      title: "AMRAP", format: "AMRAP", ...meta(),
      meta: `${timeCap} minute AMRAP`,
      movements: chosen.map((m) => toMove(m,
        m.unit === "reps" ? `${scaleReps(jitter(10), level)}` :
        m.unit === "cal" ? `${scaleReps(jitter(12), level)} cal` : `${scaleDistance(jitter(150), level)}m`)),
      timerCfg: { total: timeCap * 60 },
    });
  }

  /* A generator that hands you the same workout twice is broken. Build
     candidates until one differs from what's already on screen and from the
     last few before it — then take the best effort rather than looping forever,
     because a tightly-filtered pool genuinely may only contain one workout. */
  function signature(w) {
    if (!w) return "";
    return [w.format, w.level, w.meta, ...(w.movements || []).map((m) => m.name + m.reps)].join("|");
  }

  function generate() {
    const avoid = new Set([signature(wod), ...recentSigs.current]);
    let candidate = null;
    for (let i = 0; i < 14; i++) {
      candidate = buildWod();
      if (!avoid.has(signature(candidate))) break;
    }
    recentSigs.current = [signature(candidate), ...recentSigs.current].slice(0, 5);
    setWod(candidate);
  }

  function launchTimer() {
    if (wod) setRunnerOpen(true);
  }

  return (
    <div className="pb-28">
      <TopBar title="WOD Generator" right={
        <div className="flex gap-2">
          <button aria-label="Add" onClick={openBuilder} className="flex items-center gap-1 px-3 py-1.5 rounded-full" style={{ background: T.surface2, color: T.chalk }}>
            <Plus size={15} /> <span className="text-sm">Build</span>
          </button>
          <button aria-label="Equipment" onClick={openEquipment} className="px-3 py-1.5 rounded-full" style={{ background: T.surface2, color: T.chalk }}>
            <Settings2 size={16} />
          </button>
        </div>
      } />
      <div className="px-4">
        {/* Generate first. Everything below it is optional refinement — the
            generator is fully functional with nothing selected at all. */}
        <button aria-label="Generate another" onClick={generate} className="w-full py-4 rounded-xl flex items-center justify-center gap-2 mb-1" style={{ ...emberButton(), fontSize: 16 }}>
          <Shuffle size={18} /> {chips.length || difficulty ? "Generate WOD" : "Surprise me"}
        </button>
        <p className="text-[11px] text-center mb-1" style={{ color: T.muted }}>
          {(() => {
            const on = [];
            if (chips.length) on.push(`${chips.length} filter${chips.length > 1 ? "s" : ""}`);
            if (duration) on.push(`${duration} min`);
            if (difficulty) on.push(DIFFICULTY[difficulty].name);
            if (tempo !== "none") on.push(tempoById(tempo).notation);
            if (showLoads) on.push("weights");
            return on.length ? on.join(" · ") : "Everything rolled — press again for a new one";
          })()}
        </p>


        <button onClick={() => setDialOpen((o) => !o)}
          className="w-full flex items-center justify-center gap-1.5 py-2 mb-3">
          <span style={{ ...display, color: T.muted, fontSize: 12 }}>
            {dialOpen ? "Hide options" : "Dial it in"}
          </span>
          <ChevronRight size={13} color={T.muted}
            style={{ transform: dialOpen ? "rotate(90deg)" : "none", transition: "transform 160ms" }} />
        </button>

        {dialOpen && (
          <div className="mb-4">
            <p className="text-[11px] mb-3" style={{ color: T.muted }}>
              All optional. Anything you leave alone gets rolled for you.
            </p>
            {/* Formats get plain wording with the shorthand as a subtitle —
                "As many rounds as possible" means something to everyone,
                "AMRAP" only means something if you already knew. */}
            <SectionLabel>WORKOUT SHAPE</SectionLabel>
            <div className="grid grid-cols-2 gap-2 mb-4">
              {["AMRAP", "RFT", "EMOM", "Tabata", "Chipper", "Strength"].map((c) => {
                const on = chips.includes(c);
                return (
                  <button key={c} onClick={() => toggleChip(c)} className="p-3 rounded-xl text-left"
                    style={{ ...cardStyle(on ? T.ember : null), background: on ? T.emberDim : T.surface }}>
                    <span className="block text-[12.5px]" style={{ color: on ? T.emberInk : T.chalk, fontWeight: 700, lineHeight: 1.25 }}>
                      {plainName(c)}
                    </span>
                    <span className="block text-[10px] mt-0.5" style={{ color: T.muted }}>{c}</span>
                  </button>
                );
              })}
            </div>

            <SectionLabel>FOCUS</SectionLabel>
            <div className="flex flex-wrap mb-3">
              {CHIPS.filter((c) => !["AMRAP","RFT","EMOM","Tabata","Chipper","Strength"].includes(c))
                .map((c) => <Chip key={c} active={chips.includes(c)} onClick={() => toggleChip(c)}>{c}</Chip>)}
            </div>
        {/* How long have you got — the first thing anyone actually knows. */}
        <div className="rounded-xl p-3 mb-3" style={cardStyle()}>
          <div className="flex items-center justify-between mb-2.5">
            <span className="flex items-center gap-2">
              <TimerIcon size={15} color={duration ? T.ember : T.muted} />
              <span className="text-sm" style={{ color: T.chalk }}>How long?</span>
            </span>
            {duration && (
              <button onClick={() => setDuration(null)} className="text-[11px] px-2 py-0.5 rounded-full" style={{ background: T.surface2, color: T.muted }}>
                Clear
              </button>
            )}
          </div>
          <div className="flex gap-1.5">
            {[5, 10, 15, 20, 30].map((m) => {
              const on = duration === m;
              return (
                <button key={m} onClick={() => setDuration(on ? null : m)} className="flex-1 py-2 rounded-lg"
                  style={{
                    background: on ? T.ember : T.surface2,
                    color: on ? T.onAccent : T.chalkDim,
                    ...numeric, fontSize: 13, fontWeight: on ? 700 : 500,
                  }}>
                  {m}
                </button>
              );
            })}
          </div>
          <p className="text-[11px] mt-2" style={{ color: T.muted }}>
            {duration ? `About ${duration} minutes.` : "Nothing picked — a length gets rolled with everything else."}
          </p>
        </div>

        {/* Difficulty. Left unset, it gets rolled with the rest. */}
        <div className="rounded-xl p-3 mb-3" style={cardStyle()}>
          <div className="flex items-center justify-between mb-2.5">
            <span className="flex items-center gap-2">
              <Flame size={15} color={difficulty ? DIFFICULTY[difficulty].color : T.muted} />
              <span className="text-sm" style={{ color: T.chalk }}>Difficulty</span>
            </span>
            {difficulty && (
              <button onClick={() => setDifficulty(null)} className="text-[11px] px-2 py-0.5 rounded-full" style={{ background: T.surface2, color: T.muted }}>
                Clear
              </button>
            )}
          </div>
          <div className="flex gap-1.5">
            {DIFFICULTY_ORDER.map((id) => {
              const d = DIFFICULTY[id];
              const on = difficulty === id;
              return (
                <button key={id} onClick={() => setDifficulty(on ? null : id)} className="flex-1 py-2 rounded-lg"
                  style={{
                    background: on ? d.color : T.surface2,
                    color: on ? T.onAccent : T.chalkDim,
                    border: `1px solid ${on ? d.color : "transparent"}`,
                    ...display, fontSize: 12,
                  }}>
                  {d.name}
                </button>
              );
            })}
          </div>
          <p className="text-[11px] mt-2" style={{ color: T.muted }}>
            {difficulty ? DIFFICULTY[difficulty].blurb : "Nothing picked — difficulty gets rolled with everything else."}
          </p>
        </div>

        {/* Tempo and load are session-level choices, so they sit with the
            generator rather than on individual movements. */}
        <div className="rounded-xl p-3 mb-3" style={cardStyle()}>
          <button onClick={() => setTempoOpen((o) => !o)} className="w-full flex items-center justify-between">
            <span className="flex items-center gap-2">
              <TimerIcon size={15} color={T.ember} />
              <span className="text-sm" style={{ color: T.chalk }}>Tempo</span>
            </span>
            <span className="flex items-center gap-1">
              <span style={{ ...numeric, color: tempo === "none" ? T.muted : T.ember, fontSize: 12 }}>
                {tempoById(tempo).notation}
              </span>
              <ChevronRight size={14} color={T.muted}
                style={{ transform: tempoOpen ? "rotate(90deg)" : "none", transition: "transform 160ms" }} />
            </span>
          </button>

          {tempoOpen && (
            <div className="mt-3 space-y-1.5">
              {TEMPO_SCHEMES.map((t) => (
                <button key={t.id} onClick={() => setTempo(t.id)}
                  className="w-full text-left px-3 py-2 rounded-lg flex items-center justify-between"
                  style={{ background: tempo === t.id ? T.emberDim : T.surface2, border: `1px solid ${tempo === t.id ? T.ember + "66" : "transparent"}` }}>
                  <span>
                    <span className="text-sm" style={{ color: T.chalk }}>{t.name}</span>
                    <span className="block text-[11px]" style={{ color: T.muted }}>{t.blurb}</span>
                  </span>
                  <span style={{ ...numeric, color: tempo === t.id ? T.ember : T.muted, fontSize: 12 }}>{t.notation}</span>
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="rounded-xl p-3 mb-3" style={cardStyle()}>
          <button onClick={() => setShowLoads((v) => !v)} className="w-full flex items-center justify-between">
            <span className="flex items-center gap-2">
              <Flame size={15} color={T.amber} />
              <span className="text-sm" style={{ color: T.chalk }}>Suggest weights for me</span>
            </span>
            <span className="w-10 h-5.5 rounded-full relative" style={{ background: showLoads ? T.amber : T.line, height: 22, width: 40 }}>
              <span className="absolute top-0.5 w-4 h-4 rounded-full transition-all"
                style={{ left: showLoads ? 21 : 3, background: T.surface }} />
            </span>
          </button>
          {showLoads && (
            <>
              <div className="flex items-center gap-2 mt-3">
                {[["Light", 0.85], ["Moderate", 1.0], ["Heavy", 1.1]].map(([l, v]) => (
                  <button key={l} onClick={() => setIntensity(v)} className="flex-1 py-2 rounded-lg text-xs"
                    style={{ background: intensity === v ? T.amber : T.surface2, color: intensity === v ? T.onAccent : T.chalkDim, fontWeight: intensity === v ? 700 : 500 }}>
                    {l}
                  </button>
                ))}
              </div>
              <p className="text-[11px] mt-2" style={{ color: T.muted }}>
                Uses a logged PR where you have one, otherwise estimates from your heaviest logged set. Always a starting point, not a prescription.
              </p>
            </>
          )}
        </div>

          </div>
        )}

        {wod && (
          <div className="rounded-2xl p-5 relative overflow-hidden" style={{ ...slateStyle, backgroundImage: `${grainUrl}, linear-gradient(168deg, #232A33, #171C23)` }}>
            {(() => {
              const c = wod.level ? DIFFICULTY[wod.level].color : T.ember;
              return (
                <div className="absolute top-0 right-0 px-3 py-1" style={{ background: c + "22", borderBottomLeftRadius: 10, borderLeft: `2px solid ${c}`, borderBottom: `2px solid ${c}` }}>
                  <span style={{ ...display, color: c, fontSize: 11, letterSpacing: "0.1em" }}>{wod.difficulty}</span>
                </div>
              );
            })()}
            <div className="flex items-center gap-2">
              <h2 style={{ ...display, color: BOARD.text, fontSize: 26 }}>
                {GLOSSARY[wod.title] ? plainName(wod.title) : wod.title}
              </h2>
              <button onClick={() => toggleSavedWod(wod)}>
                <Star size={18}
                  color={savedWods.some((x) => x.key === wod.title + "|" + wod.meta) ? T.amber : T.muted}
                  fill={savedWods.some((x) => x.key === wod.title + "|" + wod.meta) ? T.amber : "none"} />
              </button>
            </div>
            <div className="mb-2 -mt-0.5" style={{ maxWidth: 170 }}><ChalkRule color="#FFFFFF" /></div>
            <p className="text-sm flex items-center gap-2" style={{ color: "rgba(243,241,234,0.7)" }}>
              {wod.meta}
              {GLOSSARY[wod.title] && (
                <Term term={wod.title} onExplain={setExplain} color="rgba(243,241,234,0.6)" size={10} />
              )}
            </p>
            {wod.note && <p className="text-[11px] mt-1" style={{ color: BOARD.amber }}>{wod.note}</p>}
            {wod.rolled?.length > 0 && (
              <p className="text-[11px] mt-1.5 flex items-center gap-1" style={{ color: BOARD.dim }}>
                <Shuffle size={11} /> Rolled: {wod.rolled.join(" · ")}
              </p>
            )}
            <div className="mb-4" />
            <div className="space-y-3">
              {wod.movements.map((m, i) => {
                const sc = getScale(m.pattern, m.scale);
                // Only lifts get a weight suggestion, and only if we have a
                // number to base it on — a guess here would be worse than silence.
                const load = wod.showLoads && !m.load
                  ? suggestLoad(m.name, m.reps, wod.intensity ?? 1, store)
                  : null;
                return (
                  <div key={i} className="flex items-start gap-3">
                    <IconBadge pattern={m.pattern} color={BOARD.text} bg="rgba(255,255,255,0.06)" size={52} />
                    <div className="flex-1">
                      <p style={{ color: BOARD.text, fontWeight: 600 }}>{m.reps ? `${m.reps} — ` : ""}{m.name}</p>
                      {m.load && <p className="text-xs" style={{ color: BOARD.dim }}>{convertLoadString(m.load, settings.units)}</p>}
                      {load && (
                        <div className="inline-flex items-center gap-1.5 mt-1 px-2 py-1 rounded-lg" style={{ background: "rgba(224,147,12,0.18)" }}>
                          <span style={{ ...numeric, color: BOARD.amber, fontSize: 13, fontWeight: 600 }}>{load.text}</span>
                          <span style={{ ...numeric, color: BOARD.dim, fontSize: 10 }}>{load.pct}% · {load.source}</span>
                        </div>
                      )}
                      <p className="text-[11px] mt-1" style={{ color: BOARD.sage }}>↓ Scale down: {sc.down}</p>
                      <p className="text-[11px]" style={{ color: BOARD.ember }}>↑ Scale up: {sc.up}</p>
                    </div>
                  </div>
                );
              })}
            </div>
            {wod.tempo && wod.tempo !== "none" && (
              <div className="mt-4"><TempoBar scheme={wod.tempo} /></div>
            )}

            <div className="mt-5 flex gap-2">
              <button aria-label="Play" onClick={launchTimer} className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2" style={{ background: BOARD.text, color: "#16181D", boxShadow: "0 3px 14px rgba(0,0,0,0.35)", ...display, fontSize: 16 }}>
                <Play size={18} /> Go
              </button>
              <button onClick={() => onLog({ type: "wod", name: wod.title, detail: wod.meta })} className="px-4 rounded-xl" style={{ background: "rgba(255,255,255,0.1)", color: BOARD.text }}>
                <Check size={18} />
              </button>
              <button onClick={() => shareWod(wod, settings.units)} className="px-4 rounded-xl" style={{ background: "rgba(255,255,255,0.1)", color: BOARD.text }}>
                <Share2 size={18} />
              </button>
            </div>
          </div>
        )}
        {!wod && (
          <div className="text-center mt-10 px-4">
            <p className="text-sm" style={{ color: T.chalkDim }}>
              Tap the button above and you'll get a full workout — you don't have to choose anything.
            </p>
            <p className="text-[11.5px] mt-2" style={{ color: T.muted }}>
              It only uses the equipment you've told us you have. Open "Dial it in" if you want to steer it.
            </p>
          </div>
        )}
      </div>
      <GlossarySheet term={explain} onClose={() => setExplain(null)} />
      {runnerOpen && wod && (
        <WorkoutRunner
          wod={wod}
          store={store}
          onExit={() => setRunnerOpen(false)}
          onFinish={(entry) => { setRunnerOpen(false); onLog(entry); }}
        />
      )}
    </div>
  );
}

/* ============================== MUSCLE GROUP LIBRARY ============================== */
function MuscleScreen({ store, jumpExercise, clearJump, onLog }) {
  const { equipment, settings, favorites, toggleFavorite, history } = store;
  const [group, setGroup] = useState(null);
  const [detail, setDetail] = useState(null);

  // Search can hand us an exercise to open directly.
  useEffect(() => {
    if (jumpExercise) { setDetail(jumpExercise); clearJump(); }
  }, [jumpExercise, clearJump]);
  const availEquip = useMemo(() => new Set(Object.keys(equipment).filter((k) => equipment[k])), [equipment]);

  if (detail) {
    const down = alternativesFor(detail, equipment, "down");
    const up = alternativesFor(detail, equipment, "up");
    const complex = isComplex(detail);

    return (
      <div className="pb-28">
        <TopBar title={detail.name} onBack={() => setDetail(null)}
          right={
            <button onClick={() => toggleFavorite("ex:" + detail.id)} aria-label="Save movement">
              <Star size={20} color={favorites.includes("ex:" + detail.id) ? T.amber : T.muted}
                fill={favorites.includes("ex:" + detail.id) ? T.amber : "none"} />
            </button>
          } />
        <div className="px-4">
          <div className="flex justify-center mb-4"><IconBadge pattern={detail.pattern} color={T.ember} bg={T.surface2} size={140} /></div>

          <p style={{ color: T.chalk }} className="mb-3">{detail.desc}</p>

          {complex && (
            <p className="text-sm italic mb-3" style={{ color: T.amber }}>
              This is a technical movement. If it doesn't feel right, take a scale-down option below.
            </p>
          )}

          {/* Demo link. Opens a live search rather than a fixed video, so it
              can't rot and always reflects what's currently on YouTube. */}
          <a href={demoSearchUrl(detail.name)} target="_blank" rel="noopener noreferrer"
            className="w-full flex items-center gap-3 p-3.5 rounded-xl mb-4" style={cardStyle(T.ember)}>
            <span className="w-9 h-9 rounded-lg flex items-center justify-center shrink-0" style={{ background: T.emberDim }}>
              <Play size={16} color={T.ember} />
            </span>
            <span className="flex-1 text-left">
              <span className="block text-sm" style={{ color: T.chalk, fontWeight: 600 }}>Watch a demo</span>
              <span className="block text-[11px]" style={{ color: T.muted }}>Opens video results for this movement</span>
            </span>
            <ChevronRight size={16} color={T.muted} />
          </a>

          <div className="rounded-xl p-3 flex items-start gap-2.5 mb-4" style={{ background: T.surface2, border: `1px solid ${T.hairline}` }}>
            <Dumbbell size={15} color={T.chalkDim} className="mt-0.5 shrink-0" />
            <p className="text-sm" style={{ color: T.chalk }}>
              <strong>Equipment:</strong>{" "}
              {detail.equip.map((eq) => EQUIP_MAP[eq]?.name.toLowerCase()).join(" or ")}.
            </p>
          </div>

          <SectionLabel>Movement pattern</SectionLabel>
          <div className="flex gap-2 mb-4">
            <div className="rounded-xl px-3 py-2.5 flex items-center gap-2" style={{ background: T.surface2 }}>
              <IconBadge pattern={detail.pattern} color={T.ember} size={30} still />
              <span className="text-xs" style={{ color: T.chalk }}>{PATTERN_LABEL[detail.pattern] || detail.pattern}</span>
            </div>
            {detail.muscle.slice(0, 2).map((m) => (
              <div key={m} className="rounded-xl px-3 py-2.5 flex items-center" style={{ background: T.surface2 }}>
                <span className="text-xs" style={{ color: T.chalkDim }}>{m}</span>
              </div>
            ))}
          </div>

          <SectionLabel>Cues</SectionLabel>
          <ul className="space-y-2 mb-4">
            {detail.cues.map((c, i) => <li key={i} className="flex items-start gap-2" style={{ color: T.chalk }}><Check size={16} color={T.ember} className="mt-1 shrink-0" /> {c}</li>)}
          </ul>

          {/* Scale options are now actual alternative movements you can open,
              not just advice — matching how a coach would offer a substitute. */}
          <SectionLabel color={T.sage}>↓ Scale down</SectionLabel>
          <div className="rounded-xl p-3 mb-2" style={{ background: T.sageDim }}>
            <p className="text-xs" style={{ color: T.chalk }}>{getScale(detail.pattern, detail.scale).down}</p>
          </div>
          {down.length > 0 && (
            <div className="space-y-1.5 mb-4">
              {down.map((alt) => (
                <button key={alt.id} onClick={() => setDetail(alt)}
                  className="w-full flex items-center gap-2.5 p-2.5 rounded-xl" style={cardStyle()}>
                  <IconBadge pattern={alt.pattern} color={T.sage} bg={T.surface2} size={36} still />
                  <span className="flex-1 text-left text-sm" style={{ color: T.chalk }}>{alt.name}</span>
                  <span className="px-2.5 py-1 rounded-lg text-[11px] flex items-center gap-1"
                    style={{ background: T.sageDim, color: T.sage, fontWeight: 600 }}>
                    Swap it
                  </span>
                </button>
              ))}
            </div>
          )}

          <SectionLabel color={T.ember}>↑ Scale up</SectionLabel>
          <div className="rounded-xl p-3 mb-2" style={{ background: T.emberDim }}>
            <p className="text-xs" style={{ color: T.chalk }}>{getScale(detail.pattern, detail.scale).up}</p>
          </div>
          {up.length > 0 ? (
            <div className="space-y-1.5 mb-4">
              {up.map((alt) => (
                <button key={alt.id} onClick={() => setDetail(alt)}
                  className="w-full flex items-center gap-2.5 p-2.5 rounded-xl" style={cardStyle()}>
                  <IconBadge pattern={alt.pattern} color={T.ember} bg={T.surface2} size={36} still />
                  <span className="flex-1 text-left text-sm" style={{ color: T.chalk }}>{alt.name}</span>
                  <span className="px-2.5 py-1 rounded-lg text-[11px]"
                    style={{ background: T.emberDim, color: T.ember, fontWeight: 600 }}>
                    Swap it
                  </span>
                </button>
              ))}
            </div>
          ) : (
            <p className="text-xs mb-4" style={{ color: T.muted }}>
              No harder variation in your library — add load, slow the tempo, or add reps instead.
            </p>
          )}

          {(() => {
            const sug = suggestNextLoad(history, detail.name, detail.pattern, settings.units);
            return sug ? (
              <div className="rounded-xl p-3 mb-4 flex items-center gap-2" style={{ background: T.amberDim }}>
                <TrendingUp size={16} color={T.amber} className="shrink-0" />
                <p className="text-xs" style={{ color: T.chalk }}>
                  Last time you logged {sug.last}. Try <strong>{sug.next}</strong> today if that felt solid.
                </p>
              </div>
            ) : null;
          })()}

          <button onClick={() => onLog({ type: "lift", name: detail.name, detail: detail.muscle.join(" · "), pattern: detail.pattern })}
            className="w-full py-3 rounded-xl flex items-center justify-center gap-2" style={{ ...emberButton(), fontSize: 16 }}>
            <Check size={18} /> Log this lift
          </button>
        </div>
      </div>
    );
  }

  if (group) {
    const list = EX.filter((e) => e.muscle.includes(group));
    return (
      <div className="pb-28">
        <TopBar title={group} onBack={() => setGroup(null)} />
        <div className="px-4 space-y-2">
          {list.map((e) => {
            const has = e.equip.some((eq) => eq === "bodyweight" || availEquip.has(eq));
            return (
              <button key={e.id} onClick={() => setDetail(e)} className="w-full flex items-center gap-3 p-3 rounded-xl" style={{ ...cardStyle(), opacity: has ? 1 : 0.45 }}>
                <IconBadge pattern={e.pattern} color={T.ember} bg={T.surface2} size={52} />
                <div className="flex-1 text-left">
                  <p style={{ color: T.chalk, fontWeight: 600 }}>{e.name}</p>
                  <p className="text-xs" style={{ color: T.muted }}>{has ? "Equipment ready" : "Needs equipment not in your list"}</p>
                </div>
                <ChevronRight color={T.muted} size={18} />
              </button>
            );
          })}
        </div>
      </div>
    );
  }

  return (
    <div className="pb-28">
      <TopBar title="Muscle Groups & Lifts" />
      <div className="px-4 grid grid-cols-2 gap-3">
        {MUSCLE_GROUPS.map((g) => {
          const rep = EX.find((e) => e.muscle.includes(g));
          return (
            <button key={g} onClick={() => setGroup(g)} className="aspect-square rounded-2xl flex flex-col items-center justify-center gap-2" style={cardStyle()}>
              <IconBadge pattern={rep?.pattern || "squat"} color={T.ember} bg={T.surface2} size={56} />
              <span style={{ color: T.chalk, fontWeight: 600 }}>{g}</span>
            </button>
          );
        })}
      </div>
    </div>
  );
}

/* ============================== YOGA ============================== */
function YogaScreen({ store, jumpArea, clearJumpArea }) {
  const [area, setArea] = useState(null);
  useEffect(() => { if (jumpArea) { setArea(jumpArea); clearJumpArea(); } }, [jumpArea, clearJumpArea]);
  const [activeIdx, setActiveIdx] = useState(null);
  const timer = useTimer(store?.settings?.sound ?? true);

  const startFlow = (poses) => { setActiveIdx(0); timer.start("countdown", { total: POSE_LIB[poses[0]].hold }); };

  useEffect(() => {
    if (activeIdx === null || !area) return;
    const poses = YOGA_AREAS[area];
    if (timer.seconds === 0 && timer.mode === "countdown" && !timer.running) {
      if (activeIdx < poses.length - 1) {
        const next = activeIdx + 1;
        const t = setTimeout(() => { setActiveIdx(next); timer.start("countdown", { total: POSE_LIB[poses[next]].hold }); }, 600);
        return () => clearTimeout(t);
      }
    }
    // eslint-disable-next-line
  }, [timer.seconds, timer.running]);

  if (area && activeIdx !== null) {
    const poses = YOGA_AREAS[area];
    const key = poses[activeIdx];
    const pose = POSE_LIB[key];
    return (
      <div className="pb-28 flex flex-col items-center" style={{ minHeight: "70vh" }}>
        <TopBar title={area} onBack={() => { timer.reset(); setActiveIdx(null); }} accent={T.sage} />
        <p style={{ ...display, color: T.sage, fontSize: 14 }}>Pose {activeIdx + 1} / {poses.length}</p>
        <div className="my-4"><IconBadge pose={pose.diagram} color={T.sage} bg={T.surface2} size={140} /></div>
        <h2 style={{ ...display, color: T.chalk, fontSize: 24 }}>{pose.name}</h2>
        <p className="text-sm px-8 text-center mt-2" style={{ color: T.muted }}>{pose.desc}</p>
        <p style={{ ...numeric, color: T.chalk, fontSize: 58, fontWeight: 600 }} className="mt-6">{fmt(timer.seconds)}</p>
        <div className="flex gap-4 mt-4">
          {timer.running ? <button aria-label="Pause" onClick={timer.pause} className="p-4 rounded-full" style={{ background: T.surface2 }}><Pause color={T.chalk} /></button>
            : <button aria-label="Play" onClick={timer.resume} className="p-4 rounded-full" style={{ background: T.sage }}><Play color={T.onAccent} /></button>}
          {activeIdx < poses.length - 1 && (
            <button onClick={() => { const n = activeIdx + 1; setActiveIdx(n); timer.start("countdown", { total: POSE_LIB[poses[n]].hold }); }} className="px-5 py-4 rounded-full text-sm" style={{ background: T.surface2, color: T.chalk }}>Skip</button>
          )}
        </div>
      </div>
    );
  }

  if (area) {
    const poses = YOGA_AREAS[area];
    const total = poses.reduce((s, k) => s + POSE_LIB[k].hold, 0);
    return (
      <div className="pb-28">
        <TopBar title={area} onBack={() => setArea(null)} accent={T.sage} />
        <div className="px-4">
          <p className="text-sm mb-3" style={{ color: T.muted }}>{poses.length} poses · ~{Math.round(total / 60)} min</p>
          <div className="space-y-2 mb-4">
            {poses.map((k) => (
              <div key={k} className="flex items-center gap-3 p-3 rounded-xl" style={cardStyle()}>
                <IconBadge pose={POSE_LIB[k].diagram} color={T.sage} bg={T.surface2} size={52} />
                <div className="flex-1">
                  <p style={{ color: T.chalk, fontWeight: 600 }}>{POSE_LIB[k].name}</p>
                  <p className="text-xs" style={{ color: T.muted }}>{POSE_LIB[k].desc}</p>
                </div>
                <span className="text-xs" style={{ color: T.sage }}>{POSE_LIB[k].hold}s</span>
              </div>
            ))}
          </div>
          <button onClick={() => startFlow(poses)} className="w-full py-3 rounded-xl flex items-center justify-center gap-2" style={{ ...emberButton(T.sage), fontSize: 16 }}>
            <Play size={18} /> Start Flow
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="pb-28">
      <TopBar title="Yoga & Mobility" accent={T.sage} />
      <div className="px-4 grid grid-cols-2 gap-3">
        {Object.keys(YOGA_AREAS).map((a) => {
          const starred = store?.favorites?.includes("yoga:" + a);
          return (
            <div key={a} className="relative aspect-square rounded-2xl" style={cardStyle()}>
              <button onClick={() => setArea(a)} className="w-full h-full flex flex-col items-center justify-center gap-2">
                <IconBadge pose={POSE_LIB[YOGA_AREAS[a][0]].diagram} color={T.sage} bg={T.surface2} size={56} />
                <span style={{ color: T.chalk, fontWeight: 600, textAlign: "center", fontSize: 14 }}>{a}</span>
              </button>
              <button onClick={() => store.toggleFavorite("yoga:" + a)} className="absolute top-2 right-2 p-1">
                <Star size={16} color={starred ? T.amber : T.muted} fill={starred ? T.amber : "none"} />
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
}

/* ============================== TOOLS ============================== */
/* ============================== CARDIO ============================== */
function CardioScreen({ store, onLog }) {
  const { equipment, settings } = store;
  const [section, setSection] = useState("movements");
  const [raceDist, setRaceDist] = useState("5k");
  const [detail, setDetail] = useState(null);
  const availEquip = useMemo(() => new Set(Object.keys(equipment).filter((k) => equipment[k])), [equipment]);
  const cardioMoves = EX.filter((e) => e.cardio);
  const accent = "#4FA3D1";
  const accentDim = "#152833";

  if (detail) {
    return (
      <div className="pb-28">
        <TopBar title={detail.name} onBack={() => setDetail(null)} accent={accent} />
        <div className="px-4">
          <div className="flex justify-center mb-4"><IconBadge pattern={detail.pattern} color={accent} bg={T.surface2} size={140} /></div>
          <p style={{ color: T.chalk }} className="mb-4">{detail.desc}</p>
          <p style={{ ...display, color: T.muted, fontSize: 13 }} className="mb-2">Cues</p>
          <ul className="space-y-2 mb-4">
            {detail.cues.map((c, i) => <li key={i} className="flex items-start gap-2" style={{ color: T.chalk }}><Check size={16} color={accent} className="mt-1 shrink-0" /> {c}</li>)}
          </ul>
          {detail.scale && (
            <>
              <p style={{ ...display, color: T.muted, fontSize: 13 }} className="mb-2">Scale It</p>
              <div className="grid grid-cols-2 gap-2 mb-4">
                <div className="rounded-xl p-3" style={{ background: T.sageDim }}>
                  <p style={{ ...display, color: T.sage, fontSize: 12 }} className="mb-1">Scale Down</p>
                  <p className="text-xs" style={{ color: T.chalk }}>{detail.scale.down}</p>
                </div>
                <div className="rounded-xl p-3" style={{ background: accentDim }}>
                  <p style={{ ...display, color: accent, fontSize: 12 }} className="mb-1">Scale Up</p>
                  <p className="text-xs" style={{ color: T.chalk }}>{detail.scale.up}</p>
                </div>
              </div>
            </>
          )}
          <div className="rounded-xl p-3 flex flex-wrap gap-2 mb-4" style={{ background: T.surface2 }}>
            {detail.equip.map((eq) => <span key={eq} className="text-xs px-2 py-1 rounded-full" style={{ background: T.surfaceLift, color: T.chalkDim, border: `1px solid ${T.hairline}` }}>{EQUIP_MAP[eq]?.icon} {EQUIP_MAP[eq]?.name}</span>)}
          </div>

          <button onClick={() => onLog({ type: "cardio", name: detail.name, detail: detail.desc, pattern: detail.pattern })}
            className="w-full py-3 rounded-xl flex items-center justify-center gap-2" style={{ ...emberButton(accent), fontSize: 16 }}>
            <Check size={18} /> Log this session
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="pb-28">
      <TopBar title="Cardio" accent={accent} />
      <div className="px-4">
        <div className="flex mb-4">
          {[["movements", "Movements"], ["metronome", "Metronome"], ["fivek", "Race Training"]].map(([id, label]) => (
            <button
              key={id}
              onClick={() => setSection(id)}
              className="flex-1 py-2 rounded-full text-xs mr-2"
              style={{ background: section === id ? accent : T.surface2, color: section === id ? T.ink : T.muted, fontWeight: section === id ? 700 : 500 }}
            >
              {label}
            </button>
          ))}
        </div>

        {section === "movements" && (
          <div className="space-y-2">
            {cardioMoves.map((e) => {
              const has = e.equip.some((eq) => eq === "bodyweight" || availEquip.has(eq));
              return (
                <button key={e.id} onClick={() => setDetail(e)} className="w-full flex items-center gap-3 p-3 rounded-xl" style={{ ...cardStyle(), opacity: has ? 1 : 0.45 }}>
                  <IconBadge pattern={e.pattern} color={accent} bg={T.surface2} size={52} />
                  <div className="flex-1 text-left">
                    <p style={{ color: T.chalk, fontWeight: 600 }}>{e.name}</p>
                    <p className="text-xs" style={{ color: T.muted }}>{has ? "Equipment ready" : "Needs equipment not in your list"}</p>
                  </div>
                  <ChevronRight color={T.muted} size={18} />
                </button>
              );
            })}
          </div>
        )}

        {section === "metronome" && (
          <div>
            <Metronome accent={accent} />
            <p className="text-xs text-center mt-4" style={{ color: T.muted }}>
              A cadence of 170-180 steps per minute is a common target for efficient running form. Set the beat and try to land a step on every click.
            </p>
          </div>
        )}

        {section === "fivek" && (
          <div>
            <div className="rounded-2xl p-4 mb-4" style={cardStyle()}>
              <p style={{ ...display, color: accent, fontSize: 15 }} className="mb-3">Pace Chart</p>
              <div className="flex gap-1 mb-3">
                {RACE_DISTANCES.map((d) => (
                  <button key={d.id} onClick={() => setRaceDist(d.id)} className="flex-1 py-1.5 rounded-full text-xs"
                    style={{ background: raceDist === d.id ? accent : T.surface2, color: raceDist === d.id ? T.ink : T.muted, fontWeight: raceDist === d.id ? 700 : 500 }}>
                    {d.label}
                  </button>
                ))}
              </div>
              <div className="grid grid-cols-3 gap-1 text-xs mb-1" style={{ color: T.muted }}>
                <span>Goal time</span><span>Per km</span><span>Per mile</span>
              </div>
              {paceRows(RACE_DISTANCES.find((d) => d.id === raceDist)).map((g) => (
                <div key={g.goal} className="grid grid-cols-3 gap-1 py-1.5 border-t" style={{ ...numeric, borderColor: T.line, color: T.chalk, fontSize: 13 }}>
                  <span style={{ fontWeight: 700 }}>{g.goal}</span><span style={{ color: T.chalkDim }}>{g.perKm}/km</span><span style={{ color: T.chalkDim }}>{g.perMile}/mi</span>
                </div>
              ))}
            </div>

            <p style={{ ...display, color: accent, fontSize: 15 }} className="mb-2">4-Week Training Plan</p>
            <div className="space-y-2 mb-4">
              {FIVEK_PLAN.map((w) => (
                <div key={w.week} className="rounded-xl p-3" style={{ background: T.surface }}>
                  <p style={{ ...display, color: T.chalk, fontSize: 13 }} className="mb-2">Week {w.week}</p>
                  <div className="text-xs space-y-1" style={{ color: T.muted }}>
                    <p><span style={{ color: T.chalk }}>Tue:</span> {w.tue}</p>
                    <p><span style={{ color: T.chalk }}>Wed:</span> {w.wed}</p>
                    <p><span style={{ color: T.chalk }}>Fri:</span> {w.fri}</p>
                    <p><span style={{ color: T.chalk }}>Sat:</span> {w.sat}</p>
                  </div>
                </div>
              ))}
            </div>

            <p style={{ ...display, color: accent, fontSize: 15 }} className="mb-2">Running Tips</p>
            <ul className="space-y-2">
              {RUNNING_TIPS.map((t, i) => (
                <li key={i} className="flex items-start gap-2 text-sm" style={{ color: T.chalk }}>
                  <Check size={16} color={accent} className="mt-0.5 shrink-0" /> {t}
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  );
}

/* The runner and full-screen timer keep a dark ground even though the app is
   light: at arm's length on a gym floor, maximum contrast wins, and the switch
   signals you've left browsing and started working. */
/* The generated workout renders on a slate board — a dark island on a light
   page — so it needs its own type colours rather than the app's. */
const BOARD = {
  text: "#F3F1EA",
  dim: "rgba(243,241,234,0.62)",
  ember: "#FF7A54",
  sage: "#8FBFA0",
  amber: "#FFC24B",
};

const RUNNER = {
  bg: "#14171C",
  text: "#F3F1EA",
  dim: "#8B93A1",
  panel: "#1E232B",
};

/* ============================== WORKOUT RUNNER ==============================
   A different mental mode deserves a different visual mode: no cards, no chrome,
   edge to edge. Arm's length legibility is the whole design brief — you're
   sweating, the phone is on the floor, and you need the clock and the current
   movement and nothing else.

   It also closes a real gap: rounds are tapped rather than remembered, so the
   session produces a structured score instead of a free-text guess afterwards.
==================================================================== */
function WorkoutRunner({ wod, store, onExit, onFinish }) {
  const { settings } = store;
  const timer = useTimer(settings.sound);
  const [rounds, setRounds] = useState(0);
  const [idx, setIdx] = useState(0);
  const [started, setStarted] = useState(false);

  const moves = wod.movements || [];
  const countsUp = wod.format === "RFT" || wod.format === "Chipper" || wod.format === "Sets x Reps";
  const accent = wod.level ? DIFFICULTY[wod.level].color : T.ember;

  const begin = () => {
    if (countsUp) timer.start("stopwatch");
    else if (wod.format === "Tabata") timer.start("interval", wod.timerCfg || { work: 20, rest: 10, rounds: 8 });
    else timer.start("countdown", wod.timerCfg || { total: 15 * 60 });
    setStarted(true);
  };

  const tapRound = () => {
    setRounds((r) => r + 1);
    setIdx(0);
    if (settings.sound) Sound.roundDone();
    if (settings.voice) Sound.say(`Round ${rounds + 1}`, true);
  };

  const nextMove = () => {
    setIdx((i) => {
      if (i + 1 >= moves.length) { tapRound(); return 0; }
      const next = i + 1;
      if (settings.sound) Sound.tick();
      if (settings.voice) Sound.say(moves[next].name, true);
      return next;
    });
  };

  const finish = () => {
    timer.pause();
    Sound.stopSpeech();
    if (settings.sound) Sound.finish();
    const score = countsUp
      ? fmt(timer.seconds)
      : `${rounds} rounds${idx > 0 ? ` + ${idx}` : ""}`;
    onFinish({
      type: "wod",
      name: wod.title,
      detail: wod.meta,
      result: score,
      // Structured so history can chart and compare it later, rather than
      // storing a string nobody can parse.
      score: { rounds, partial: idx, seconds: timer.seconds, kind: countsUp ? "time" : "rounds" },
    });
  };

  const current = moves[idx];
  const upcoming = moves[(idx + 1) % (moves.length || 1)];

  return (
    <div className="fixed inset-0 z-50 flex flex-col" style={{ background: RUNNER.bg }}>
      {/* Minimal chrome */}
      <div className="flex items-center justify-between px-5 pt-5">
        <button onClick={() => { Sound.stopSpeech(); onExit(); }} aria-label="Leave workout"><X size={24} color={RUNNER.dim} /></button>
        <span style={{ ...display, color: accent, fontSize: 13, letterSpacing: "0.14em" }}>{wod.title}</span>
        <button onClick={finish} style={{ ...display, color: RUNNER.text, fontSize: 13 }}>Finish</button>
      </div>

      {!started ? (
        <div className="flex-1 flex flex-col items-center justify-center px-8">
          <p style={{ ...display, color: RUNNER.dim, fontSize: 13, letterSpacing: "0.16em" }}>{wod.meta}</p>
          <div className="my-8 w-full space-y-2.5">
            {moves.map((m, i) => (
              <div key={i} className="flex items-baseline gap-3">
                <span style={{ ...numeric, color: accent, fontSize: 20, fontWeight: 600, minWidth: 62 }}>{m.reps}</span>
                <span style={{ ...display, color: RUNNER.text, fontSize: 22 }}>{m.name}</span>
              </div>
            ))}
          </div>
          <button aria-label="Play" onClick={begin} className="w-full py-5 rounded-2xl flex items-center justify-center gap-2"
            style={{ ...emberButton(accent), fontSize: 22 }}>
            <Play size={24} /> Start
          </button>
        </div>
      ) : (
        <>
          {/* The clock, at arm's length size */}
          <div className="flex-1 flex flex-col items-center justify-center">
            <p style={{ ...numeric, color: RUNNER.text, fontSize: 92, fontWeight: 600, lineHeight: 1 }}>
              {fmt(timer.seconds)}
            </p>
            {timer.mode === "interval" && (
              <p style={{ ...display, color: timer.phase === "rest" ? T.sage : accent, fontSize: 22 }}>
                {timer.phase === "rest" ? "Rest" : "Work"}
              </p>
            )}
            {!countsUp && wod.format !== "Tabata" && (
              <p className="text-sm mt-1" style={{ color: RUNNER.dim }}>remaining</p>
            )}
          </div>

          {/* Current movement, big. Next one ghosted underneath. */}
          {current && (
            <button onClick={nextMove} className="px-6 pb-2 text-left w-full">
              <p style={{ ...numeric, color: accent, fontSize: 22, fontWeight: 600 }}>{current.reps}</p>
              <p style={{ ...display, color: RUNNER.text, fontSize: 34, lineHeight: 1.05 }}>{current.name}</p>
              {moves.length > 1 && (
                <p style={{ ...display, color: RUNNER.dim, fontSize: 17, opacity: 0.5 }} className="mt-2">
                  next · {upcoming.name}
                </p>
              )}
              <p className="text-[11px] mt-2" style={{ color: RUNNER.dim }}>Tap to advance</p>
            </button>
          )}

          {/* Round counter — the score, built as you go */}
          <div className="px-5 pb-8 pt-3 flex items-center gap-3">
            <button onClick={tapRound} className="flex-1 py-4 rounded-2xl flex items-center justify-center gap-3"
              style={{ background: RUNNER.panel, border: `1px solid ${accent}44` }}>
              <span style={{ ...numeric, color: accent, fontSize: 30, fontWeight: 600 }}>{rounds}</span>
              <span style={{ ...display, color: RUNNER.textDim, fontSize: 14 }}>
                {countsUp ? "rounds done" : "rounds +1"}
              </span>
            </button>
            <button onClick={() => setRounds((r) => Math.max(0, r - 1))}
              className="w-14 h-14 rounded-2xl flex items-center justify-center"
              style={{ background: RUNNER.panel }} aria-label="Undo round">
              <span style={{ ...numeric, color: RUNNER.dim, fontSize: 22 }}>−</span>
            </button>
            <button onClick={timer.running ? timer.pause : timer.resume}
              className="w-14 h-14 rounded-2xl flex items-center justify-center"
              style={{ background: timer.running ? T.surface2 : accent }}
              aria-label={timer.running ? "Pause" : "Resume"}>
              {timer.running ? <Pause size={22} color={RUNNER.text} /> : <Play size={22} color={T.onAccent} />}
            </button>
          </div>
        </>
      )}
    </div>
  );
}

/* ============================== WARM-UP SCREEN ============================== */
function WarmupScreen({ store, onBack, onLog }) {
  const accent = T.amber;
  const { settings, favorites, toggleFavorite } = store;
  const [wu, setWu] = useState(() => generateWarmup(null));
  const [running, setRunning] = useState(false);
  const timer = useTimer(settings.sound);

  const start = () => { timer.start("countdown", { total: wu.minutes * 60 }); setRunning(true); };

  if (running) {
    return (
      <div className="pb-28 flex flex-col items-center" style={{ minHeight: "80vh" }}>
        <TopBar title="Warm-up" onBack={() => { timer.reset(); setRunning(false); }} accent={accent} />
        <p style={{ ...numeric, color: T.chalk, fontSize: 72, fontWeight: 600 }} className="mt-6">{fmt(timer.seconds)}</p>
        <p style={{ ...display, color: accent, fontSize: 14 }} className="mb-6">{wu.minutes} min AMRAP · easy pace</p>
        <div className="w-full px-4 space-y-2">
          {wu.movements.map((m, i) => (
            <div key={i} className="flex items-center gap-3 p-3 rounded-xl" style={cardStyle()}>
              <span className="px-2.5 py-1 rounded-lg shrink-0" style={{ background: T.amberDim }}>
                <span style={{ ...numeric, color: accent, fontSize: 12, fontWeight: 600 }}>{m.reps}</span>
              </span>
              <span className="flex-1 text-sm" style={{ color: T.chalk }}>
                {m.name}{m.each && <span style={{ color: T.muted }}> (each side)</span>}
              </span>
            </div>
          ))}
        </div>
        <div className="flex gap-4 mt-8">
          {timer.running
            ? <button aria-label="Pause" onClick={timer.pause} className="p-4 rounded-full" style={{ background: T.surface2 }}><Pause color={T.chalk} /></button>
            : <button aria-label="Play" onClick={timer.resume} className="p-4 rounded-full" style={{ background: accent }}><Play color={T.onAccent} /></button>}
          <button onClick={() => { timer.reset(); setRunning(false); onLog({ type: "warmup", name: "Warm-up", detail: `${wu.minutes} min AMRAP` }); }}
            className="px-5 py-4 rounded-full text-sm" style={{ background: T.surface2, color: T.chalk }}>
            Done
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="pb-28">
      <TopBar title="Warm-up" onBack={onBack} accent={accent} />
      <div className="px-4">
        <p style={{ ...display, color: T.chalk, fontSize: 22 }} className="text-center mt-2 mb-4">
          <span style={{ ...numeric, color: accent }}>{wu.minutes}</span> minute AMRAP
          <span style={{ color: T.muted, fontSize: 16 }}> (easy pace) of:</span>
        </p>

        <div className="space-y-2 mb-4">
          {wu.movements.map((m, i) => (
            <div key={i} className="flex items-center gap-3 p-3.5 rounded-xl" style={cardStyle()}>
              <span className="px-3 py-1.5 rounded-lg shrink-0" style={{ background: T.amberDim }}>
                <span style={{ ...numeric, color: accent, fontSize: 13, fontWeight: 600 }}>{m.reps} reps</span>
              </span>
              <span className="flex-1 text-sm" style={{ color: T.chalk }}>
                {m.name}{m.each && <span className="italic" style={{ color: T.muted }}> (each side)</span>}
              </span>
              <IconBadge pattern={m.pattern} color={accent} size={34} still />
            </div>
          ))}
        </div>

        <p className="text-sm text-center mb-5" style={{ color: T.muted }}>
          Keep an easy pace — this is a warm-up, not the workout.
        </p>

        <div className="flex gap-2">
          <button onClick={() => setWu(generateWarmup(wu))} className="px-4 rounded-xl" style={{ background: T.surface2, color: T.chalk }}
            aria-label="Generate a different warm-up">
            <Shuffle size={18} />
          </button>
          <button onClick={() => toggleFavorite("warmup:" + wu.movements.map((m) => m.name).join("|"))}
            className="px-4 rounded-xl" style={{ background: T.surface2 }} aria-label="Save warm-up">
            <Star size={18} color={favorites.includes("warmup:" + wu.movements.map((m) => m.name).join("|")) ? accent : T.chalk}
              fill={favorites.includes("warmup:" + wu.movements.map((m) => m.name).join("|")) ? accent : "none"} />
          </button>
          <button aria-label="Play" onClick={start} className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2" style={{ ...emberButton(accent), fontSize: 16 }}>
            <Play size={18} /> Start
          </button>
        </div>
      </div>
    </div>
  );
}

/* ============================== TIMER SETUP ============================== */
function TimerSetup({ store, onBack }) {
  const accent = T.ember;
  const [explain, setExplain] = useState(null);
  const { settings } = store;
  const [type, setType] = useState("AMRAP");
  const [mins, setMins] = useState(10);
  const [secs, setSecs] = useState(0);
  const [rounds, setRounds] = useState(8);
  const [restSecs, setRestSecs] = useState(10);
  const [beepEachMinute, setBeepEachMinute] = useState(false);
  const timer = useTimer(settings.sound);
  const [live, setLive] = useState(false);

  const TYPES = [
    { id: "AMRAP", term: "AMRAP" },
    { id: "FOR TIME", term: "For Time" },
    { id: "EMOM", term: "EMOM" },
    { id: "TABATA", term: "Tabata" },
  ];
  const total = type === "TABATA" ? (mins * 60 + secs + restSecs) * rounds : mins * 60 + secs;

  const start = () => {
    if (type === "FOR TIME") timer.start("stopwatch");
    else if (type === "TABATA") timer.start("interval", { work: mins * 60 + secs, rest: restSecs, rounds });
    else timer.start("countdown", { total: mins * 60 + secs });
    setLive(true);
  };

  if (live) {
    return <TimerOverlay timer={timer} title={type} onClose={() => setLive(false)} targetRounds={rounds} />;
  }

  const Stepper = ({ label, value, set, min = 0, max = 60, step = 1 }) => (
    <div className="flex-1">
      <p className="text-[11px] mb-1 italic" style={{ color: T.muted }}>{label}</p>
      <div className="rounded-xl py-3 text-center" style={{ background: T.surface2, border: `1px solid ${T.hairline}` }}>
        <span style={{ ...numeric, color: T.chalk, fontSize: 24, fontWeight: 600 }}>{value}</span>
      </div>
    </div>
  );

  return (
    <div className="pb-28">
      <TopBar title="Setup Timer" onBack={onBack} accent={accent} />
      <div className="px-4">
        <SectionLabel>Timer type</SectionLabel>
        <div className="grid grid-cols-2 gap-2 mb-5">
          {TYPES.map((t) => {
            const on = type === t.id;
            return (
              <button key={t.id} onClick={() => setType(t.id)} className="p-3 rounded-xl text-left"
                style={{ ...cardStyle(on ? accent : null), background: on ? T.emberDim : undefined }}>
                <span className="flex items-center gap-2">
                  <span className="w-4 h-4 rounded-full flex items-center justify-center shrink-0"
                    style={{ border: `2px solid ${on ? accent : T.line}` }}>
                    {on && <span className="w-2 h-2 rounded-full" style={{ background: accent }} />}
                  </span>
                  <span className="text-[12.5px]" style={{ color: on ? T.emberInk : T.chalk, fontWeight: 700, lineHeight: 1.2 }}>
                    {plainName(t.term)}
                  </span>
                </span>
                <span className="block text-[10px] mt-1.5" style={{ color: T.muted }}>{t.id}</span>
              </button>
            );
          })}
        </div>

        {type !== "FOR TIME" && (
          <>
            <SectionLabel>{type === "TABATA" ? "Work interval" : "Work time"}</SectionLabel>
            <div className="rounded-xl p-3.5 mb-3" style={cardStyle()}>
              <div className="flex gap-2 items-end mb-3">
                <Stepper label="Minutes" value={mins} />
                <Stepper label="Seconds" value={secs} />
                <div className="flex gap-2">
                  <button onClick={() => { if (secs > 0) setSecs(secs - 5); else if (mins > 0) { setMins(mins - 1); setSecs(55); } }}
                    className="w-11 h-11 rounded-xl" style={{ background: T.surface2, color: T.chalk, fontSize: 20 }} aria-label="Decrease">−</button>
                  <button onClick={() => { if (secs >= 55) { setSecs(0); setMins(mins + 1); } else setSecs(secs + 5); }}
                    className="w-11 h-11 rounded-xl" style={{ ...emberButton(), fontSize: 20 }} aria-label="Increase">+</button>
                </div>
              </div>
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-[11px]" style={{ color: T.muted }}>Quick choose:</span>
                {(type === "TABATA" ? [[0, 20], [0, 30], [0, 45], [1, 0]] : [[6, 0], [12, 0], [16, 0], [20, 0]]).map(([m, sc]) => (
                  <button key={`${m}:${sc}`} onClick={() => { setMins(m); setSecs(sc); }}
                    className="px-2 py-0.5 rounded-md"
                    style={{ ...numeric, color: accent, fontSize: 12, background: T.emberDim }}>
                    {String(m).padStart(2, "0")}:{String(sc).padStart(2, "0")}
                  </button>
                ))}
              </div>
            </div>
          </>
        )}

        {type === "TABATA" && (
          <div className="rounded-xl p-3.5 mb-3" style={cardStyle()}>
            <div className="flex items-center justify-between mb-3">
              <span className="text-sm" style={{ color: T.chalk }}>Rest between</span>
              <div className="flex items-center gap-2">
                <button onClick={() => setRestSecs(Math.max(5, restSecs - 5))} className="w-9 h-9 rounded-lg" style={{ background: T.surface2, color: T.chalk }}>−</button>
                <span style={{ ...numeric, color: T.chalk, fontSize: 16, width: 42, textAlign: "center" }}>{restSecs}s</span>
                <button onClick={() => setRestSecs(restSecs + 5)} className="w-9 h-9 rounded-lg" style={{ background: T.surface2, color: T.chalk }}>+</button>
              </div>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm" style={{ color: T.chalk }}>Rounds</span>
              <div className="flex items-center gap-2">
                <button onClick={() => setRounds(Math.max(1, rounds - 1))} className="w-9 h-9 rounded-lg" style={{ background: T.surface2, color: T.chalk }}>−</button>
                <span style={{ ...numeric, color: T.chalk, fontSize: 16, width: 42, textAlign: "center" }}>{rounds}</span>
                <button onClick={() => setRounds(rounds + 1)} className="w-9 h-9 rounded-lg" style={{ background: T.surface2, color: T.chalk }}>+</button>
              </div>
            </div>
          </div>
        )}

        {type === "EMOM" && (
          <button onClick={() => setBeepEachMinute((v) => !v)} className="w-full flex items-center justify-between p-3.5 rounded-xl mb-3" style={cardStyle()}>
            <span className="text-sm" style={{ color: T.chalk }}>Play sound every minute</span>
            <span className="rounded-full relative" style={{ background: beepEachMinute ? accent : T.line, width: 40, height: 22 }}>
              <span className="absolute top-0.5 w-4 h-4 rounded-full" style={{ left: beepEachMinute ? 21 : 3, background: T.surface }} />
            </span>
          </button>
        )}

        <button onClick={() => setExplain(TYPES.find((t) => t.id === type)?.term)}
          className="w-full flex items-center justify-center gap-1.5 mb-3 py-2" aria-label="Explain this timer type">
          <span className="text-[12px]" style={{ color: T.ember }}>What does {type} mean?</span>
        </button>

        <p className="text-center text-sm mb-3" style={{ color: T.muted }}>
          Total time: <span style={{ ...numeric, color: T.chalk, fontWeight: 600 }}>
            {type === "FOR TIME" ? "no cap" : fmt(total)}
          </span>
        </p>

        <button aria-label="Play" onClick={start} className="w-full py-3.5 rounded-xl flex items-center justify-center gap-2" style={{ ...emberButton(), fontSize: 17 }}>
          <Play size={19} /> Start
        </button>
      </div>
      <GlossarySheet term={explain} onClose={() => setExplain(null)} />
    </div>
  );
}

function RoutinePlayer({ list, title, accent, onBack, soundOn = true }) {
  const [activeIdx, setActiveIdx] = useState(null);
  const timer = useTimer(soundOn);

  useEffect(() => {
    if (activeIdx === null) return;
    if (timer.seconds === 0 && timer.mode === "countdown" && !timer.running) {
      if (activeIdx < list.length - 1) {
        const next = activeIdx + 1;
        const t = setTimeout(() => { setActiveIdx(next); timer.start("countdown", { total: list[next].seconds }); }, 600);
        return () => clearTimeout(t);
      }
    }
    // eslint-disable-next-line
  }, [timer.seconds, timer.running]);

  if (activeIdx !== null) {
    const item = list[activeIdx];
    return (
      <div className="pb-28 flex flex-col items-center" style={{ minHeight: "70vh" }}>
        <TopBar title={title} onBack={() => { timer.reset(); setActiveIdx(null); }} accent={accent} />
        <p style={{ ...display, color: accent, fontSize: 14 }}>Step {activeIdx + 1} / {list.length}</p>
        <div className="my-4"><IconBadge pattern={item.pattern} pose={item.pose} color={accent} size={130} /></div>
        <h2 style={{ ...display, color: T.chalk, fontSize: 24 }}>{item.name}</h2>
        <p className="text-sm px-8 text-center mt-2" style={{ color: T.muted }}>{item.desc}</p>
        <p style={{ ...numeric, color: T.chalk, fontSize: 58, fontWeight: 600 }} className="mt-6">{fmt(timer.seconds)}</p>
        <div className="flex gap-4 mt-4">
          {timer.running ? (
            <button aria-label="Pause" onClick={timer.pause} className="p-4 rounded-full" style={{ background: T.surface2 }}><Pause color={T.chalk} /></button>
          ) : (
            <button aria-label="Play" onClick={timer.resume} className="p-4 rounded-full" style={{ background: accent }}><Play color={T.onAccent} /></button>
          )}
          {activeIdx < list.length - 1 && (
            <button onClick={() => { const n = activeIdx + 1; setActiveIdx(n); timer.start("countdown", { total: list[n].seconds }); }} className="px-5 py-4 rounded-full text-sm" style={{ background: T.surface2, color: T.chalk }}>Skip</button>
          )}
        </div>
      </div>
    );
  }

  const total = list.reduce((s, k) => s + k.seconds, 0);
  return (
    <div className="pb-28">
      <TopBar title={title} onBack={onBack} accent={accent} />
      <div className="px-4">
        <p className="text-sm mb-3" style={{ color: T.muted }}>{list.length} moves · ~{Math.round(total / 60) || 1} min</p>
        <div className="space-y-2 mb-4">
          {list.map((item, i) => (
            <div key={i} className="flex items-center gap-3 p-3 rounded-xl" style={cardStyle()}>
              <IconBadge pattern={item.pattern} pose={item.pose} color={accent} bg={T.surface2} size={52} />
              <div className="flex-1">
                <p style={{ color: T.chalk, fontWeight: 600 }}>{item.name}</p>
                <p className="text-xs" style={{ color: T.muted }}>{item.desc}</p>
              </div>
              <span className="text-xs" style={{ color: accent }}>{item.seconds}s</span>
            </div>
          ))}
        </div>
        <button
          onClick={() => { setActiveIdx(0); timer.start("countdown", { total: list[0].seconds }); }}
          className="w-full py-3 rounded-xl flex items-center justify-center gap-2"
          style={{ ...emberButton(accent), fontSize: 16 }}
        >
          <Play size={18} /> Start
        </button>
      </div>
    </div>
  );
}

function ToolsScreen({ store, openBuilder, onLog }) {
  const { settings } = store;
  const [tool, setTool] = useState(null);
  const timer = useTimer(settings.sound);
  const [customMin, setCustomMin] = useState(10);
  const [rmWeight, setRmWeight] = useState(60);
  const [rmReps, setRmReps] = useState(5);
  const oneRM = Math.round(rmWeight * (1 + rmReps / 30));
  const pcts = [50, 60, 70, 75, 80, 85, 90, 95];

  if (tool === "timer") return <TimerSetup store={store} onBack={() => { timer.reset(); setTool(null); }} />;

  if (tool === "rm") {
    return (
      <div className="pb-28">
        <TopBar title="Max lift calculator" onBack={() => setTool(null)} accent={T.amber} />
        <div className="px-4">
          <label className="text-sm" style={{ color: T.muted }}>Weight lifted ({settings.units})</label>
          <input type="number" value={rmWeight} onChange={(e) => setRmWeight(Number(e.target.value))} className="w-full p-3 rounded-xl mb-3 mt-1" style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }} />
          <label className="text-sm" style={{ color: T.muted }}>Reps performed</label>
          <input type="number" value={rmReps} onChange={(e) => setRmReps(Number(e.target.value))} className="w-full p-3 rounded-xl mb-4 mt-1" style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }} />
          <div className="rounded-2xl p-4 text-center mb-4" style={{ background: T.amberDim }}>
            <p className="text-sm" style={{ color: T.amberInk }}>Estimated max for one rep</p>
            <p style={{ ...numeric, color: T.chalk, fontSize: 42, fontWeight: 600 }}>{oneRM} <span style={{ fontSize: 20 }}>{settings.units}</span></p>
          </div>
          <div className="grid grid-cols-4 gap-2">
            {pcts.map((p) => (
              <div key={p} className="rounded-xl p-2 text-center" style={{ background: T.surface }}>
                <p className="text-xs" style={{ color: T.muted }}>{p}%</p>
                <p style={{ color: T.chalk, fontWeight: 700 }}>{Math.round(oneRM * (p / 100))}</p>
              </div>
            ))}
          </div>
        </div>
      </div>
    );
  }

  if (tool === "warmup") return <WarmupScreen store={store} onBack={() => setTool(null)} onLog={onLog} />;
  if (tool === "cooldown") return <RoutinePlayer list={COOLDOWN} title="Cool-Down" accent={T.sage} soundOn={settings.sound} onBack={() => setTool(null)} />;
  if (tool === "rest") return (
    <div className="pb-28">
      <TopBar title="Rest Timer" onBack={() => setTool(null)} accent={T.amber} />
      <div className="px-4 mt-4"><RestTimer soundOn={settings.sound} /></div>
    </div>
  );

  return (
    <div className="pb-28">
      <TopBar title="Tools" accent={T.amber} />
      <div className="px-4 space-y-3">
        <button onClick={() => setTool("timer")} className="w-full flex items-center gap-3 p-4 rounded-xl" style={cardStyle()}>
          <Clock color={T.amber} />
          <span className="flex-1 text-left">
            <span className="block" style={{ color: T.chalk, fontWeight: 600 }}>Timer</span>
            <span className="block text-[11px]" style={{ color: T.muted }}>AMRAP · For time · EMOM · Tabata</span>
          </span>
          <ChevronRight size={15} color={T.muted} />
        </button>
        <button onClick={() => setTool("rm")} className="w-full flex items-center gap-3 p-4 rounded-xl" style={cardStyle()}><Flame color={T.amber} /> <span className="flex-1 text-left">
            <span className="block" style={{ color: T.chalk, fontWeight: 600 }}>Max lift calculator</span>
            <span className="block text-[11px]" style={{ color: T.muted }}>Estimate your heaviest single from a set you've done</span>
          </span></button>
        <button onClick={() => setTool("warmup")} className="w-full flex items-center gap-3 p-4 rounded-xl" style={cardStyle()}>
          <Sunrise color={T.amber} />
          <span className="flex-1 text-left">
            <span className="block" style={{ color: T.chalk, fontWeight: 600 }}>Warm-Up</span>
            <span className="block text-[11px]" style={{ color: T.muted }}>A fresh easy-pace AMRAP every time</span>
          </span>
          <ChevronRight size={15} color={T.muted} />
        </button>
        <button onClick={() => setTool("cooldown")} className="w-full flex items-center gap-3 p-4 rounded-xl" style={cardStyle()}><Moon color={T.sage} /> <span style={{ color: T.chalk, fontWeight: 600 }}>Cool-Down Routine</span></button>
        <button onClick={() => setTool("rest")} className="w-full flex items-center gap-3 p-4 rounded-xl" style={cardStyle()}><TimerIcon color={T.amber} /> <span style={{ color: T.chalk, fontWeight: 600 }}>Rest Timer</span></button>
        <button aria-label="Add" onClick={openBuilder} className="w-full flex items-center gap-3 p-4 rounded-xl" style={cardStyle()}><Plus color={T.ember} /> <span style={{ color: T.chalk, fontWeight: 600 }}>Build a Custom WOD</span></button>
      </div>
    </div>
  );
}

/* Pull a time out of a free-text result so benchmark times become real PRs.
   Accepts "8:42", "8m42s", or a bare number of seconds. Returns null for
   rep/round results like "6 rounds" so those never masquerade as times. */
function parseResultTime(result) {
  if (!result) return null;
  const str = String(result).trim().toLowerCase();
  if (/round|rep|cal\b/.test(str)) return null;

  const colon = str.match(/^(\d{1,3}):([0-5]\d)$/);
  if (colon) return parseInt(colon[1], 10) * 60 + parseInt(colon[2], 10);

  const ms = str.match(/^(\d{1,3})\s*m(?:in)?\s*([0-5]?\d)?\s*s?$/);
  if (ms) return parseInt(ms[1], 10) * 60 + (ms[2] ? parseInt(ms[2], 10) : 0);

  const bare = str.match(/^(\d{2,4})\s*s(?:ec)?$/);
  if (bare) return parseInt(bare[1], 10);

  return null;
}

/* ============================== EXPORT / IMPORT ==============================
   Training history is the one thing here that can't be regenerated, so it needs
   a way out. CSV for spreadsheets, JSON for a full restore.
==================================================================== */
function downloadFile(filename, text, mime) {
  // Inside a claude.ai artifact a plain download link is inert; the viewer's
  // own save dialog (the downloads capability) does the job instead.
  if (typeof window !== "undefined" && window.claude?.use) {
    claudeCapability("downloads").then((d) => {
      if (d) d.save({ filename, data: new Blob([text], { type: mime }) }).catch(() => {});
    });
    return true;
  }
  try {
    const blob = new Blob([text], { type: mime });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    return true;
  } catch (e) {
    return false;
  }
}

function csvCell(v) {
  if (v === null || v === undefined) return "";
  const str = String(v);
  // Quote anything containing a delimiter, quote, or newline; double inner quotes.
  const needsQuote = str.includes(",") || str.includes('"') || str.includes("\n");
  return needsQuote ? '"' + str.split('"').join('""') + '"' : str;
}

function historyToCsv(history, units) {
  const head = ["date", "type", "name", "detail", "result", `load_${units}`, "rpe", "notes"];
  const rows = history.map((h) => [
    new Date(h.date).toISOString(),
    h.type || "",
    h.name || "",
    h.detail || "",
    h.result || "",
    h.loadKg ? (units === "lb" ? Math.round(h.loadKg * KG_TO_LB) : Math.round(h.loadKg)) : "",
    h.rpe || "",
    h.notes || "",
  ]);
  return [head, ...rows].map((r) => r.map(csvCell).join(",")).join("\n");
}

function metricsToCsv(metrics, units) {
  const head = ["date", `bodyweight_${units}`, "sleep_hours", "soreness_1_5", "note"];
  const rows = metrics.map((m) => [
    m.day,
    m.weightKg ? (units === "lb" ? Math.round(m.weightKg * KG_TO_LB * 10) / 10 : Math.round(m.weightKg * 10) / 10) : "",
    m.sleep || "",
    m.soreness || "",
    m.note || "",
  ]);
  return [head, ...rows].map((r) => r.map(csvCell).join(",")).join("\n");
}

function buildBackup(store) {
  return JSON.stringify({
    app: "FitForge",
    version: 1,
    exportedAt: new Date().toISOString(),
    history: store.history,
    prs: store.prs,
    metrics: store.metrics,
    favorites: store.favorites,
    savedWods: store.savedWods,
    plan: store.plan,
    settings: store.settings,
  }, null, 2);
}

/* ============================== CHARTS ==============================
   Small inline SVG charts. No library — these are simple enough that a
   dependency would cost more than it saves, and they inherit the theme.
==================================================================== */
function LineChart({ points, color = T.ember, height = 120, label, formatY }) {
  if (!points || points.length < 2) {
    return <p className="text-xs text-center py-8" style={{ color: T.muted }}>Not enough data yet.</p>;
  }
  const W = 320, H = height, pad = 6;
  const ys = points.map((p) => p.y);
  const min = Math.min(...ys), max = Math.max(...ys);
  const span = max - min || 1;
  const xStep = (W - pad * 2) / (points.length - 1);
  const coords = points.map((p, i) => [
    pad + i * xStep,
    pad + (1 - (p.y - min) / span) * (H - pad * 2),
  ]);
  const path = coords.map((c, i) => `${i ? "L" : "M"}${c[0].toFixed(1)},${c[1].toFixed(1)}`).join(" ");
  const area = `${path} L${coords[coords.length - 1][0].toFixed(1)},${H - pad} L${coords[0][0].toFixed(1)},${H - pad} Z`;

  return (
    <div>
      <div className="flex justify-between text-[10px] mb-1" style={{ color: T.muted }}>
        <span>{label}</span>
        <span>{formatY ? formatY(min) : min} – {formatY ? formatY(max) : max}</span>
      </div>
      <svg viewBox={`0 0 ${W} ${H}`} width="100%" height={H} preserveAspectRatio="none">
        <path d={area} fill={color} opacity="0.13" />
        <path d={path} fill="none" stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
        {coords.map((c, i) => (
          <circle key={i} cx={c[0]} cy={c[1]} r={i === coords.length - 1 ? 3.5 : 1.8} fill={color} />
        ))}
      </svg>
    </div>
  );
}

/* ============================== TRAINING CONTEXT ==============================
   Turns raw history into the compact brief a coach actually needs: what you've
   done lately, what you've neglected, how hard it's felt, and what you said
   about it. Computed locally and deterministically — the model is never asked
   to do arithmetic on your logs, only to interpret an accurate summary.
==================================================================== */
function daysAgo(iso) {
  return Math.floor((Date.now() - new Date(iso).getTime()) / 86400000);
}

function buildTrainingContext(store) {
  const { history, prs, settings, plan, equipment, coachLog, metrics } = store;
  const recent = history.slice(0, 25);

  // The coach must only ever prescribe gear the athlete actually owns.
  const availableEquipment = EQUIPMENT.filter((e) => equipment[e.id]).map((e) => e.name);
  const availableMovements = EX
    .filter((e) => e.equip.some((q) => q === "bodyweight" || equipment[q]))
    .map((e) => e.name);

  // Which muscle groups the logged work actually hit.
  const groupCounts = {};
  const patternLastSeen = {};
  recent.forEach((h) => {
    const ex = EX.find((e) => e.name === h.name);
    if (ex) ex.muscle.forEach((m) => { groupCounts[m] = (groupCounts[m] || 0) + 1; });
    const pat = h.pattern || ex?.pattern;
    if (pat && patternLastSeen[pat] === undefined) patternLastSeen[pat] = daysAgo(h.date);
  });

  const neglected = MUSCLE_GROUPS.filter((m) => !groupCounts[m]);
  const rpes = recent.filter((h) => h.rpe).map((h) => h.rpe);
  const avgRpe = rpes.length ? (rpes.reduce((a, b) => a + b, 0) / rpes.length).toFixed(1) : null;

  // A week of sessions with nothing easy in it is the classic overreach pattern.
  const last7 = history.filter((h) => daysAgo(h.date) <= 7);
  const hardLast7 = last7.filter((h) => h.rpe && h.rpe >= 8).length;

  const notes = recent.filter((h) => h.notes).slice(0, 8)
    .map((h) => `${daysAgo(h.date)}d ago (${h.name}): ${h.notes}`);

  // Plan adherence: what the week asked for vs what actually got logged.
  let planAdherence = null;
  if (plan) {
    const ws = mondayOf();
    const done = new Set();
    history.forEach((h) => {
      const d = new Date(h.date);
      if (d >= ws) done.add((d.getDay() + 6) % 7);
    });
    const todayI = (new Date().getDay() + 6) % 7;
    planAdherence = {
      plannedThisWeek: plan.days.filter((d) => d !== "Rest").length,
      completedThisWeek: plan.days.filter((d, i) => d !== "Rest" && done.has(i)).length,
      missedSoFar: plan.days.filter((d, i) => d !== "Rest" && i < todayI && !done.has(i)),
      todayCallsFor: plan.days[todayI],
    };
  }

  const byType = (t) => history.filter((h) => h.type === t);
  const lastOfType = (t) => (byType(t).length ? daysAgo(byType(t)[0].date) : null);

  return {
    goal: settings.goal,
    level: settings.level,
    units: settings.units,
    availableEquipment,
    availableMovements,
    tempoSchemesAvailable: TEMPO_SCHEMES.filter((t) => t.secs).map((t) => ({ id: t.id, notation: t.notation, use: t.blurb })),
    yogaFlowsAvailable: Object.keys(YOGA_AREAS),
    breathPatternsAvailable: BREATH_PATTERNS.map((b) => b.name),
    daysSinceCardio: lastOfType("cardio"),
    daysSinceYogaOrMobility: lastOfType("yoga"),
    sessionTypeCounts: {
      wod: byType("wod").length,
      lift: byType("lift").length,
      cardio: byType("cardio").length,
      yoga: byType("yoga").length,
    },
    plan: plan ? plan.days.join(", ") : "none set",
    planAdherence,
    totalSessions: history.length,
    sessionsLast7Days: last7.length,
    hardSessionsLast7Days: hardLast7,
    daysSinceLastSession: history.length ? daysAgo(history[0].date) : null,
    avgRecentRPE: avgRpe,
    recentSessions: recent.slice(0, 12).map((h) => ({
      daysAgo: daysAgo(h.date),
      name: h.name,
      result: h.result || null,
      loadKg: h.loadKg || null,
      rpe: h.rpe || null,
    })),
    muscleGroupCounts: groupCounts,
    neglectedGroups: neglected,
    daysSincePattern: patternLastSeen,
    personalRecords: Object.entries(prs).map(([k, v]) => ({
      lift: k,
      value: v.unit === "time" ? fmt(v.value) : `${Math.round(v.value)}kg`,
      daysAgo: daysAgo(v.date),
    })),
    athleteNotes: notes,

    // Body signals the coach should weigh when judging readiness.
    recentCheckIns: (metrics || []).slice(0, 10).map((m) => ({
      daysAgo: daysAgo(m.date),
      bodyweightKg: m.weightKg ? Math.round(m.weightKg * 10) / 10 : null,
      sleepHours: m.sleep || null,
      soreness1to5: m.soreness || null,
      note: m.note || null,
    })),

    // What this coach previously told them, and whether it appears to have
    // happened. Lets the coach follow up instead of restarting each time.
    previousAdvice: (coachLog || []).slice(0, 6).map((a) => {
      const followed = a.type === "session"
        ? history.some((h) => new Date(h.date) > new Date(a.date) && h.name === a.title)
        : null;
      return {
        daysAgo: daysAgo(a.date),
        type: a.type,
        summary: a.summary,
        title: a.title || null,
        appearsCompleted: followed,
      };
    }),
  };
}

/* Resolve a free-text movement name from the coach onto the library.
   Exact match, then containment either way, then token overlap — so "KB swing",
   "Kettlebell Swings (heavy)", and "swings" all land on the same entry instead
   of silently defaulting to a squat. */
const STOPWORDS = new Set(["the", "a", "with", "and", "of", "for", "each", "per", "side", "alternating", "heavy", "light"]);
function tokenize(str) {
  return (str || "")
    .toLowerCase()
    .replace(new RegExp("\\([^)]*\\)", "g"), " ")   // drop parenthetical asides
    .replace(new RegExp("[^a-z\\s-]", "g"), " ")      // punctuation to spaces
    .split(new RegExp("[\\s-]+"))
    .filter((w) => w.length > 2 && !STOPWORDS.has(w));
}
function matchExercise(name) {
  if (!name) return null;
  const n = name.toLowerCase().trim();

  const exact = EX.find((e) => e.name.toLowerCase() === n);
  if (exact) return exact;

  // Prefer the most specific containment hit: "Romanian Deadlifts" must land on
  // Romanian Deadlift, not on plain Deadlift just because it appears first.
  const contains = EX
    .filter((e) => {
      const en = e.name.toLowerCase();
      return en.includes(n) || n.includes(en);
    })
    .sort((a, b) => b.name.length - a.name.length)[0];
  if (contains) return contains;

  const want = tokenize(name);
  if (!want.length) return null;
  let best = null;
  let bestScore = 0;
  EX.forEach((e) => {
    const have = tokenize(e.name);
    const overlap = want.filter((w) => have.some((h) => h.startsWith(w) || w.startsWith(h))).length;
    const score = overlap / Math.max(want.length, have.length);
    if (score > bestScore) { bestScore = score; best = e; }
  });
  return bestScore >= 0.5 ? best : null;
}

/* ============================== AI COACH ==============================
   Calls the model with the locally-computed brief. Two modes: a read on where
   you are, and a concrete next session. Both return strict JSON so the result
   can be rendered as UI (and a generated workout can be loaded into the timer)
   rather than dumped as a wall of text.
==================================================================== */
/* Capabilities a claude.ai artifact viewer can grant (dictation, speech,
   Claude itself). Resolves null everywhere else, so callers fall back. */
const capCache = {};
function claudeCapability(name) {
  if (typeof window === "undefined" || !window.claude?.use) return Promise.resolve(null);
  if (!capCache[name]) capCache[name] = window.claude.use(name).catch(() => null);
  return capCache[name];
}

async function askCoach(mode, context, extra = "", history = []) {
  const shared = `You are an experienced strength and conditioning coach reviewing one athlete's training log.
The data below was computed directly from their logs and is accurate — do not recalculate it, just interpret it.
HARD CONSTRAINT: only prescribe movements the athlete can actually perform with the equipment
listed in availableEquipment. Prefer names from availableMovements so the app can match them.
Never prescribe a barbell, machine, or apparatus that is not in their equipment list.

ATHLETE DATA:
${JSON.stringify(context, null, 2)}

Coaching principles: respect recovery, avoid piling hard sessions back to back, address neglected
movement patterns, and take the athlete's own notes seriously — especially anything about pain,
tightness, or a movement that felt wrong. Never program through pain; work around it and say so.

You have advised this athlete before — see previousAdvice, including whether each prescribed
session appears to have been completed. Build on that history: follow up on what you asked for,
acknowledge what they did, and notice patterns of avoidance without nagging. If they have skipped
the same thing repeatedly, say so plainly once and adapt rather than repeating the identical ask.

Weigh recentCheckIns when judging readiness: short sleep, high soreness, or a sharp bodyweight
change are reasons to pull back, and worth naming out loud when they drive your advice.

Recovery counts as training. The app has yoga flows (yogaFlowsAvailable) and paced breathing
(breathPatternsAvailable). When the data shows accumulated fatigue, several hard sessions in a
row, a long gap since any mobility work, or notes mentioning tightness or poor sleep, prescribe a
yoga flow or breathing practice as the actual session rather than adding more hard work.
Be specific and concise. Speak directly to the athlete as "you".`;

  const prompts = {
    review: `${shared}

Give an honest read on their training. Respond ONLY with JSON, no markdown fences:
{
  "headline": "one short sentence on where they stand",
  "doingWell": ["2-3 specific things, referencing their actual data"],
  "watchOut": ["1-3 specific risks or gaps, referencing their actual data"],
  "focusNext": "one sentence on what the next 1-2 weeks should emphasize",
  "recoveryAdvice": "one sentence on whether they need more recovery work, and which yoga flow or breathing pattern to use if so"
}`,

    session: `${shared}

${extra ? `The athlete also said: "${extra}"\n` : ""}
Design their next single session. Respond ONLY with JSON, no markdown fences:
{
  "title": "short name for the session",
  "why": "one sentence explaining why this session, given their data",
  "format": "AMRAP | RFT | EMOM | Strength | Chipper | Intervals | Recovery",
  "sessionType": "training" or "recovery",
  "yogaFlow": "exact name from yogaFlowsAvailable, or null",
  "breathwork": "exact name from breathPatternsAvailable, or null",
  "meta": "the time domain or set/rep scheme, e.g. '15 minute AMRAP' or '5 sets of 5'",
  "tempo": "one id from tempoSchemesAvailable, or null if tempo doesn't matter today",
  "tempoWhy": "one short clause on why that tempo, or null",
  "movements": [{"name": "movement name", "reps": "prescription", "percent1RM": number 50-100 or null, "note": "optional short cue or scaling note"}],
  "coachNote": "one or two sentences of guidance for the day"
}
Prefer movements from common gym vocabulary. Keep it to 3-6 movements.

Set a tempo only when it serves the session's purpose — slow eccentrics for hypertrophy, pauses to
fix a sticking point, explosive work for power. Leave it null for conditioning and high-rep work,
where a prescribed tempo just gets ignored.

Give percent1RM only for barbell strength movements where personalRecords shows you have a number
to work from. Leave it null for bodyweight, conditioning, and anything you'd be guessing at.
If this should be a recovery day, set sessionType to "recovery", name a yogaFlow and/or breathwork,
and leave movements as an empty array.`,

    chat: `${shared}

You are mid-conversation with this athlete. Answer their question directly and specifically,
using their data where it's relevant. Two to four sentences unless they ask for more. Plain
prose — no JSON, no markdown headers, no bullet lists. If they ask you to change a session you
gave them, give the adjusted version in plain sentences.`,

    debrief: `${shared}

They just finished this session and wrote: "${extra}"

Respond ONLY with JSON, no markdown fences:
{
  "response": "2-3 sentences reacting to how that session went, specific to what they wrote",
  "adjustment": "one concrete thing to change or repeat next time"
}`,
  };

  // Chat carries the running thread so the coach can hold context across turns;
  // the structured modes are one-shot.
  const messages = mode === "chat"
    ? [{ role: "user", content: prompts.chat }, ...history, { role: "user", content: extra }]
    : [{ role: "user", content: prompts[mode] }];

  // Published as a claude.ai artifact, the page reaches Claude through the
  // viewer's own session instead of a direct API call (which needs a key).
  let text = null;
  const sample = await claudeCapability("sample");
  if (sample) {
    const out = await sample(messages, { cache: false });
    text = out.text || "";
  } else {
    const res = await fetch("https://api.anthropic.com/v1/messages", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ model: "claude-sonnet-4-6", max_tokens: 1000, messages }),
    });
    const data = await res.json();
    text = data.content.filter((c) => c.type === "text").map((c) => c.text).join("");
  }

  if (mode === "chat") return text.trim();
  const clean = text.replace(/```json|```/g, "").trim();
  return JSON.parse(clean);
}

function CoachScreen({ store, onLoadWod, onLog, onOpenYoga, onOpenBreath }) {
  const accent = T.violet;
  const accentDim = T.violetDim;
  const [mode, setMode] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [review, setReview] = useState(null);
  const [session, setSession] = useState(null);
  const [request, setRequest] = useState("");
  const [view, setView] = useState("coach");     // coach | chat
  const [draft, setDraft] = useState("");
  const [chatBusy, setChatBusy] = useState(false);
  const threadEnd = useRef(null);

  const { history, thread, addMessage, clearThread, recordAdvice, coachLog } = store;
  const ctx = useMemo(() => buildTrainingContext(store), [store.history, store.prs, store.settings, store.plan]);

  const run = async (which, extra) => {
    setBusy(true); setError(null); setMode(which);
    try {
      const out = await askCoach(which, ctx, extra);
      if (which === "review") {
        setReview(out);
        recordAdvice({ type: "review", summary: out.focusNext || out.headline });
      }
      if (which === "session") {
        setSession(out);
        recordAdvice({ type: "session", title: out.title, summary: `${out.title} — ${out.why || out.meta}` });
      }
    } catch (e) {
      setError("Couldn't reach the coach just now. Check your connection and try again.");
    }
    setBusy(false);
  };

  const send = async () => {
    const text = draft.trim();
    if (!text || chatBusy) return;
    setDraft("");
    addMessage({ role: "user", content: text });
    setChatBusy(true);
    try {
      // Pass prior turns so the coach can hold the thread.
      const prior = thread.map((m) => ({ role: m.role, content: m.content }));
      const reply = await askCoach("chat", ctx, text, prior);
      addMessage({ role: "assistant", content: reply });
    } catch (e) {
      addMessage({ role: "assistant", content: "I couldn't reach you just then — try that again in a moment." });
    }
    setChatBusy(false);
  };

  useEffect(() => { threadEnd.current?.scrollIntoView({ behavior: "smooth" }); }, [thread, chatBusy]);

  if (view === "chat") {
    return (
      <div className="pb-28 flex flex-col" style={{ minHeight: "100vh" }}>
        <TopBar title="Talk to your coach" onBack={() => setView("coach")} accent={accent}
          right={thread.length > 0 && (
            <button onClick={clearThread} className="text-xs px-3 py-1.5 rounded-full" style={{ background: T.surface2, color: T.muted }}>Clear</button>
          )} />
        <div className="flex-1 px-4 space-y-3 pb-4">
          {thread.length === 0 && (
            <div className="rounded-2xl p-4 mt-2" style={{ background: accentDim, border: `1px solid ${accent}44` }}>
              <p className="text-sm mb-2" style={{ color: T.chalk }}>
                Ask me anything about your training. I can see your log, your PRs, your notes, and what I've told you before.
              </p>
              <div className="flex flex-wrap gap-2 mt-3">
                {["Why am I not getting stronger?", "Swap today's run for something else", "Is my shoulder ready for overhead work?", "How should I train this week?"].map((q) => (
                  <button key={q} onClick={() => setDraft(q)} className="text-[11px] px-2.5 py-1.5 rounded-full" style={{ background: T.surface2, color: accent }}>
                    {q}
                  </button>
                ))}
              </div>
            </div>
          )}
          {thread.map((m, i) => (
            <div key={i} className={m.role === "user" ? "flex justify-end" : "flex justify-start"}>
              <div className="max-w-[85%] rounded-2xl px-3.5 py-2.5"
                style={{
                  background: m.role === "user" ? accent : T.surface,
                  color: m.role === "user" ? T.onAccent : T.chalk,
                  border: m.role === "user" ? "none" : `1px solid ${T.hairline}`,
                  boxShadow: m.role === "user" ? "none" : "0 1px 8px rgba(22,24,29,0.05)",
                  borderBottomLeftRadius: m.role === "user" ? 18 : 6,
                  borderBottomRightRadius: m.role === "user" ? 6 : 18,
                }}>
                <p className="text-sm whitespace-pre-wrap">{m.content}</p>
              </div>
            </div>
          ))}
          {chatBusy && (
            <div className="flex justify-start">
              <div className="rounded-2xl px-3.5 py-2.5" style={cardStyle()}>
                <p className="text-sm" style={{ color: T.muted }}>Thinking…</p>
              </div>
            </div>
          )}
          <div ref={threadEnd} />
        </div>
        <div className="fixed left-0 right-0 px-4 py-3 flex gap-2" style={{ bottom: 68, background: T.ink, borderTop: `1px solid ${T.line}` }}>
          <input
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={(e) => { if (e.key === "Enter") send(); }}
            placeholder="Ask your coach…"
            className="flex-1 p-3 rounded-xl text-sm"
            style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }}
          />
          <button aria-label="Send" onClick={send} disabled={chatBusy || !draft.trim()} className="px-4 rounded-xl"
            style={{ background: draft.trim() ? accent : T.surface2, color: draft.trim() ? T.ink : T.muted }}>
            <ArrowRight size={18} />
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="pb-28">
      <TopBar title="AI Coach" accent={accent} />
      <div className="px-4">
        {/* The coach, presented as a presence rather than a settings panel.
            Showing what it can see is what makes its advice feel earned. */}
        <div className="rounded-[22px] p-4 mb-3" style={{ ...cardStyle(), background: `linear-gradient(160deg, ${accentDim}, ${T.surface} 70%)`, border: `1px solid ${accent}22` }}>
          <div className="flex items-center gap-3 mb-3">
            <span className="w-11 h-11 rounded-full shrink-0" style={{
              background: "radial-gradient(circle at 34% 30%, #E5C8FF, #9B6BD6 58%, #6B45A0)",
              boxShadow: `0 0 18px ${accent}55`,
            }} />
            <div className="flex-1">
              <p style={{ color: T.chalk, fontWeight: 700, fontSize: 15 }}>Your coach</p>
              <p className="text-[11px]" style={{ color: T.muted }}>
                {ctx.totalSessions ? `Read your last ${Math.min(ctx.totalSessions, 12)} sessions` : "No history to read yet"}
              </p>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-y-1.5 text-[12px]">
            {[
              ["Sessions", ctx.totalSessions],
              ["Last 7 days", `${ctx.sessionsLast7Days} (${ctx.hardSessionsLast7Days} hard)`],
              ["Avg effort", ctx.avgRecentRPE ? `${ctx.avgRecentRPE}/10` : "—"],
              ["Your notes", ctx.athleteNotes.length],
            ].map(([k, v]) => (
              <React.Fragment key={k}>
                <span style={{ color: T.muted }}>{k}</span>
                <span style={{ ...numeric, color: T.chalk, fontWeight: 600 }}>{v}</span>
              </React.Fragment>
            ))}
          </div>

          {ctx.neglectedGroups.length > 0 && (
            <p className="text-[11px] mt-2.5 px-2.5 py-1.5 rounded-lg" style={{ background: T.amberDim, color: T.amberInk }}>
              Untouched lately: {ctx.neglectedGroups.join(", ")}
            </p>
          )}
          {coachLog.length > 0 && (
            <p className="text-[11px] mt-2" style={{ color: T.muted }}>
              Remembers {coachLog.length} previous {coachLog.length === 1 ? "conversation" : "conversations"}
            </p>
          )}
        </div>

        {history.length === 0 && (
          <p className="text-sm text-center mb-4" style={{ color: T.muted }}>
            Log a session or two first — the coach works off your history, and with none it can only guess.
          </p>
        )}

        <button onClick={() => setView("chat")}
          className="w-full flex items-center gap-3 p-3 rounded-2xl mb-3"
          style={{ background: accentDim, border: `1px solid ${accent}44` }}>
          <Sparkles size={18} color={accent} />
          <span className="flex-1 text-left text-sm" style={{ color: T.chalk }}>
            {thread.length ? `Continue your conversation (${thread.length} messages)` : "Talk to your coach"}
          </span>
          <ChevronRight size={16} color={T.muted} />
        </button>

        <div className="flex gap-2 mb-4">
          <button onClick={() => run("review")} disabled={busy}
            className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2"
            style={{ background: accent, color: T.onAccent, ...display, fontSize: 14, opacity: busy ? 0.6 : 1 }}>
            <Sparkles size={16} /> Review my training
          </button>
        </div>

        <div className="rounded-2xl p-4 mb-4" style={cardStyle()}>
          <p style={{ ...display, color: T.chalk, fontSize: 14 }} className="mb-2">Ask for a session</p>
          <input value={request} onChange={(e) => setRequest(e.target.value)}
            placeholder="Anything to factor in? e.g. 40 min, legs are wrecked"
            className="w-full p-3 rounded-xl mb-2 text-sm" style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }} />
          <button onClick={() => run("session", request)} disabled={busy}
            className="w-full py-3 rounded-xl flex items-center justify-center gap-2"
            style={{ background: T.surface2, color: T.chalk, ...display, fontSize: 14, opacity: busy ? 0.6 : 1 }}>
            <Sparkles size={16} /> Build my next workout
          </button>
        </div>

        {busy && (
          <p className="text-sm text-center py-6" style={{ color: accent }}>Reading your log…</p>
        )}
        {error && (
          <div className="rounded-xl p-3 mb-4" style={{ background: T.emberDim }}>
            <p className="text-sm" style={{ color: T.chalk }}>{error}</p>
          </div>
        )}

        {!busy && mode === "review" && review && (
          <div className="rounded-2xl p-5 mb-4" style={cardStyle(accent)}>
            <p style={{ ...display, color: T.chalk, fontSize: 18 }} className="mb-3">{review.headline}</p>
            <p style={{ ...display, color: T.sage, fontSize: 12 }} className="mb-1">Working</p>
            <ul className="mb-3 space-y-1">
              {review.doingWell?.map((x, i) => (
                <li key={i} className="flex gap-2 text-sm" style={{ color: T.chalk }}><Check size={15} color={T.sage} className="mt-0.5 shrink-0" />{x}</li>
              ))}
            </ul>
            <p style={{ ...display, color: T.amber, fontSize: 12 }} className="mb-1">Watch</p>
            <ul className="mb-3 space-y-1">
              {review.watchOut?.map((x, i) => (
                <li key={i} className="flex gap-2 text-sm" style={{ color: T.chalk }}><span style={{ color: T.amber }}>!</span>{x}</li>
              ))}
            </ul>
            <div className="rounded-xl p-3 mb-2" style={{ background: accentDim }}>
              <p className="text-sm" style={{ color: T.chalk }}>{review.focusNext}</p>
            </div>
            {review.recoveryAdvice && (
              <div className="rounded-xl p-3 flex items-start gap-2" style={{ background: T.sageDim }}>
                <Wind size={15} color={T.sage} className="mt-0.5 shrink-0" />
                <p className="text-sm" style={{ color: T.chalk }}>{review.recoveryAdvice}</p>
              </div>
            )}
          </div>
        )}

        {!busy && mode === "session" && session && (
          <div className="rounded-2xl p-5" style={cardStyle(accent)}>
            <h2 style={{ ...display, color: T.chalk, fontSize: 24 }}>{session.title}</h2>
            <p className="text-sm" style={{ color: T.muted }}>{session.meta}</p>
            <p className="text-xs mt-2 mb-4" style={{ color: accent }}>{session.why}</p>
            <div className="space-y-2 mb-4">
              {session.movements?.map((m, i) => {
                const hit = matchExercise(m.name);
                return (
                  <div key={i} className="flex items-center gap-3 p-3 rounded-xl" style={{ background: T.surface2 }}>
                    <IconBadge pattern={hit?.pattern || "core"} color={accent} size={38} />
                    <div className="flex-1">
                      <p style={{ color: T.chalk, fontWeight: 600 }}>{m.reps} — {m.name}</p>
                      {m.note && <p className="text-xs mt-0.5" style={{ color: T.muted }}>{m.note}</p>}
                      {!hit && <p className="text-[11px] mt-0.5" style={{ color: T.amber }}>Not in your library — check the movement before you start</p>}
                    </div>
                  </div>
                );
              })}
            </div>
            {session.tempo && (
              <div className="mb-4">
                <TempoBar scheme={session.tempo} color={accent} />
                {session.tempoWhy && (
                  <p className="text-[11px] mt-1.5" style={{ color: accent }}>{session.tempoWhy}</p>
                )}
              </div>
            )}

            {session.coachNote && (
              <div className="rounded-xl p-3 mb-4" style={{ background: accentDim }}>
                <p className="text-sm" style={{ color: T.chalk }}>{session.coachNote}</p>
              </div>
            )}
            {(session.yogaFlow || session.breathwork) && (
              <div className="space-y-2 mb-4">
                {session.yogaFlow && YOGA_AREAS[session.yogaFlow] && (
                  <button onClick={() => onOpenYoga(session.yogaFlow)}
                    className="w-full flex items-center gap-3 p-3 rounded-xl" style={{ background: T.sageDim, border: `1px solid ${T.sage}44` }}>
                    <IconBadge pose="yoga" color={T.sage} size={38} />
                    <span className="flex-1 text-left text-sm" style={{ color: T.chalk }}>
                      Yoga: <strong>{session.yogaFlow}</strong>
                    </span>
                    <ChevronRight size={16} color={T.muted} />
                  </button>
                )}
                {session.breathwork && (
                  <button onClick={onOpenBreath}
                    className="w-full flex items-center gap-3 p-3 rounded-xl" style={{ background: "#16262B", border: "1px solid #6FB6C944" }}>
                    <Wind size={18} color="#6FB6C9" />
                    <span className="flex-1 text-left text-sm" style={{ color: T.chalk }}>
                      Breathwork: <strong>{session.breathwork}</strong>
                    </span>
                    <ChevronRight size={16} color={T.muted} />
                  </button>
                )}
              </div>
            )}

            <div className="flex gap-2">
              {(session.movements || []).length > 0 && (
              <button
                onClick={() => onLoadWod({
                  title: session.title,
                  format: session.format || "AMRAP",
                  meta: session.meta,
                  difficulty: "Coach",
                  tempo: session.tempo || "none",
                  movements: (session.movements || []).map((m) => {
                    const hit = matchExercise(m.name);
                    return {
                      name: m.name,
                      reps: m.reps,
                      percent1RM: m.percent1RM || null,
                      pattern: hit?.pattern || "core",
                      prop: hit?.prop,
                      scale: hit?.scale,
                    };
                  }),
                  timerCfg: { total: 15 * 60 },
                })}
                className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2"
                style={{ background: T.chalk, color: T.onAccent, ...display, fontSize: 15 }}>
                <Play size={17} /> Do it
              </button>
              )}
              <button onClick={() => onLog({ type: session.sessionType === "recovery" ? "yoga" : "wod", name: session.title, detail: session.meta })}
                className={(session.movements || []).length > 0 ? "px-4 rounded-xl" : "flex-1 py-3 rounded-xl flex items-center justify-center gap-2"}
                style={{ background: T.surface2, color: T.chalk }}>
                <Check size={18} />{(session.movements || []).length === 0 && <span style={{ ...display, fontSize: 15 }}>Log it</span>}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

/* ============================== SHARE ============================== */
function shareWod(wod, units) {
  const lines = [
    wod.title,
    wod.meta,
    "",
    ...wod.movements.map((m) => `- ${m.reps ? m.reps + " " : ""}${m.name}${m.load ? " @ " + convertLoadString(m.load, units) : ""}`),
    "",
    "via FitForge",
  ];
  const text = lines.join("\n");
  if (navigator.share) {
    navigator.share({ title: wod.title, text }).catch(() => {});
  } else if (navigator.clipboard) {
    navigator.clipboard.writeText(text).catch(() => {});
  }
}

/* ============================== BREATH TRAINER ==============================
   Paced breathing with a visual guide that expands on the inhale and contracts
   on the exhale, so the pattern can be followed with eyes closed or open.

   Includes both slow-breathing patterns and the Wim Hof style retention method.
   The retention protocol is genuinely riskier than the others — rapid breathing
   drops CO2 and can cause fainting, which is why it sits behind an explicit
   acknowledgement screen, is never timed against a target, and always lets the
   athlete end a hold the moment they want air.
==================================================================== */
const BREATH_PATTERNS = [
  {
    id: "box", name: "Box Breathing", tag: "Focus / pre-workout", group: "Focus",
    phases: [["Inhale", 4], ["Hold", 4], ["Exhale", 4], ["Hold", 4]],
    about: "Equal counts on all four sides. Used to steady nerves and sharpen focus before a hard effort.",
  },
  {
    id: "478", name: "4-7-8 Breathing", tag: "Wind down / sleep", group: "Wind down",
    phases: [["Inhale", 4], ["Hold", 7], ["Exhale", 8]],
    about: "A long exhale relative to the inhale, which nudges the body toward rest. Good after training or before bed.",
  },
  {
    id: "coherent", name: "Coherent Breathing", tag: "Recovery", group: "Recovery",
    phases: [["Inhale", 5.5], ["Exhale", 5.5]],
    about: "Roughly five and a half breaths per minute. Simple, sustainable, and easy to hold for ten minutes.",
  },
  {
    id: "physio", name: "Physiological Sigh", tag: "Fast reset", group: "Recovery",
    phases: [["Inhale", 3], ["Second short inhale", 1], ["Long exhale", 6]],
    about: "A double inhale then a long exhale. The quickest way to drop stress between sets or after a hard round.",
  },
  {
    id: "extended", name: "Extended Exhale", tag: "Cool-down", group: "Wind down",
    phases: [["Inhale", 4], ["Exhale", 8]],
    about: "Exhale twice as long as the inhale. Pairs well with the cool-down stretches.",
  },
  {
    id: "resonant", name: "Resonant 6", tag: "Recovery", group: "Recovery",
    phases: [["Inhale", 5], ["Exhale", 5]],
    about: "Six breaths a minute, the rate most people's heart-rate variability peaks at. Easier to hold than 5.5 if counting half-seconds annoys you.",
  },
  {
    id: "triangle", name: "Triangle Breathing", tag: "Focus", group: "Focus",
    phases: [["Inhale", 4], ["Hold", 4], ["Exhale", 4]],
    about: "Box breathing without the second hold. A gentler way in if the empty-lung pause feels uncomfortable.",
  },
  {
    id: "ratio", name: "1:2 Ratio", tag: "Wind down", group: "Wind down",
    phases: [["Inhale", 5], ["Exhale", 10]],
    about: "A long, slow exhale with no holds. Reliable for dropping the heart rate after a hard session.",
  },
  {
    id: "warmup", name: "Pre-Lift Primer", tag: "Pre-workout", group: "Focus",
    phases: [["Inhale", 2], ["Hold", 1], ["Sharp exhale", 1]],
    about: "Quick and punchy, no long holds. Builds alertness before a heavy set without the lightheadedness of rapid breathing.",
  },
  {
    id: "brace", name: "Bracing Breath", tag: "Under the bar", group: "Focus",
    phases: [["Breathe into the belly", 3], ["Brace and hold", 4], ["Release", 3]],
    about: "The breath you take before a heavy rep: air into the belly, brace the whole trunk, hold through the lift. Practise it away from the bar first.",
  },
  {
    id: "ladder", name: "Exhale Ladder", tag: "Breath control", group: "Training",
    phases: [["Inhale", 4], ["Exhale", 6], ["Inhale", 4], ["Exhale", 8], ["Inhale", 4], ["Exhale", 10]],
    about: "Each exhale gets longer than the last. Builds tolerance for slow breathing without forcing one long hold.",
  },
  {
    id: "sleep", name: "Sleep Wind-Down", tag: "Before bed", group: "Wind down",
    phases: [["Inhale", 4], ["Hold", 2], ["Exhale", 8], ["Rest", 2]],
    about: "A long exhale with a short pause after it. Built to be boring — that's the point when you're trying to fall asleep.",
  },
  {
    id: "recovery", name: "Between Rounds", tag: "Mid-workout", group: "Recovery",
    phases: [["Nasal inhale", 2], ["Exhale", 4]],
    about: "Short and nasal, for the rest between intervals. Clears carbon dioxide faster than gasping through the mouth.",
  },
  {
    id: "altnostril", name: "Alternate Nostril", tag: "Calm focus", group: "Training",
    phases: [["Inhale left", 4], ["Hold", 2], ["Exhale right", 6], ["Inhale right", 4], ["Hold", 2], ["Exhale left", 6]],
    about: "Close one nostril with a thumb, switch sides each cycle. Slow, deliberate, and hard to rush — which is most of the benefit.",
  },

  // --- Focus ---
  {
    id: "tactical", name: "Tactical Breathing", tag: "Steady nerves", group: "Focus",
    phases: [["Inhale", 4], ["Hold", 4], ["Exhale", 4], ["Hold", 4], ["Inhale", 4], ["Hold", 4], ["Exhale", 4], ["Hold", 4]],
    about: "Box breathing run as two linked cycles before a max attempt. Long enough to settle, short enough to hold your place in the queue.",
  },
  {
    id: "count", name: "Counted Breath", tag: "Concentration", group: "Focus",
    phases: [["Inhale", 4], ["Exhale", 6], ["Inhale", 4], ["Exhale", 6], ["Inhale", 4], ["Exhale", 8]],
    about: "Count each exhale and start over if you lose track. The counting is the practice, not the breathing.",
  },
  {
    id: "sniff", name: "Double Sniff", tag: "Sharpen up", group: "Focus",
    phases: [["Sniff", 1], ["Sniff again", 1], ["Exhale slowly", 5]],
    about: "Two quick nasal inhales, one long exhale. Wakes you up between sets without spiking the heart rate.",
  },

  // --- Recovery ---
  {
    id: "nasal", name: "Nasal Only", tag: "Aerobic base", group: "Recovery",
    phases: [["Inhale through nose", 4], ["Exhale through nose", 6]],
    about: "Mouth closed the whole time. Uncomfortable at first, which is the signal you were over-breathing.",
  },
  {
    id: "buteyko", name: "Reduced Breathing", tag: "Air hunger", group: "Recovery",
    phases: [["Small inhale", 3], ["Small exhale", 4], ["Pause", 3]],
    about: "Deliberately shallow with a short pause. You should feel mild air hunger — not distress. Ease off if it becomes uncomfortable.",
  },
  {
    id: "humming", name: "Humming Exhale", tag: "Calm down", group: "Recovery",
    phases: [["Inhale", 4], ["Hum out", 8]],
    about: "Hum audibly all the way through the exhale. The vibration lengthens the breath naturally without you forcing it.",
  },
  {
    id: "postwod", name: "Post-WOD Reset", tag: "After the buzzer", group: "Recovery",
    phases: [["Inhale", 3], ["Exhale", 5], ["Inhale", 3], ["Exhale", 7]],
    about: "For the two minutes flat on your back after a hard piece. Starts near where your breathing already is, then slows it.",
  },

  // --- Wind down ---
  {
    id: "moon", name: "Left Nostril Only", tag: "Evening", group: "Wind down",
    phases: [["Inhale left", 4], ["Exhale left", 8]],
    about: "Close the right nostril throughout. Traditionally the cooling, settling side — a simpler option than alternating.",
  },
  {
    id: "bodyscan", name: "Scan & Release", tag: "Tension release", group: "Wind down",
    phases: [["Inhale into a tight spot", 4], ["Hold", 2], ["Exhale and let it go", 8]],
    about: "Pick one tight area per cycle — jaw, shoulders, hips — and direct the exhale there. Move somewhere new each breath.",
  },
  {
    id: "sighrelease", name: "Long Sigh", tag: "Let go", group: "Wind down",
    phases: [["Inhale", 4], ["Audible sigh out", 9]],
    about: "Let the exhale be loud and unforced. The sound stops you rationing the air on the way out.",
  },

  // --- Training ---
  {
    id: "co2", name: "CO₂ Tolerance", tag: "Breath control", group: "Training",
    requiresAck: true,
    phases: [["Inhale", 4], ["Exhale", 6], ["Hold empty", 5], ["Inhale", 4], ["Exhale", 6], ["Hold empty", 8]],
    about: "Short holds on an empty lung, getting longer. Builds comfort with carbon dioxide, which is what actually drives the urge to breathe. Never in water.",
  },
  {
    id: "cadence", name: "Running Cadence", tag: "For runners", group: "Training",
    phases: [["Inhale · 3 steps", 3], ["Exhale · 2 steps", 2]],
    about: "A 3:2 pattern for easy running — the odd number keeps the foot strike at the exhale alternating between legs.",
  },
  {
    id: "pyramid", name: "Pyramid", tag: "Progressive", group: "Training",
    phases: [["Inhale", 3], ["Hold", 3], ["Exhale", 3], ["Inhale", 4], ["Hold", 4], ["Exhale", 4], ["Inhale", 5], ["Hold", 5], ["Exhale", 5]],
    about: "Counts climb 3, 4, 5 within a single cycle, then start over. Harder than it looks by the top step.",
  },
  {
    id: "wimhof", name: "Wim Hof Method", tag: "Advanced · requires care", group: "Training",
    rounds: 3,
    powerBreaths: 30,
    breathIn: 1.6,
    breathOut: 1.4,
    recoveryHold: 15,
    requiresAck: true,
    about: "Rounds of 30 deep breaths, then an exhale hold for as long as is comfortable, then a 15-second inhale hold. Holds are never timed against a target — you end them whenever you want air.",
  },
];

/* Wim Hof style rounds. Structurally different from the paced patterns: the
   retention hold counts UP with no target, ended by the athlete, and the whole
   thing is gated behind an acknowledgement. */
function WimHofRunner({ pattern, accent, accentDim, soundOn, onExit, onLog }) {
  const STAGES = { BREATHS: "breaths", RETENTION: "retention", RECOVERY: "recovery", DONE: "done" };
  const [round, setRound] = useState(1);
  const [stage, setStage] = useState(STAGES.BREATHS);
  const [breathNum, setBreathNum] = useState(1);
  const [inhaling, setInhaling] = useState(true);
  const [t, setT] = useState(0);          // seconds within the current stage
  const [running, setRunning] = useState(true);
  const [holdTimes, setHoldTimes] = useState([]);
  const tick = useRef(null);

  useEffect(() => {
    if (!running || stage === STAGES.DONE) return;
    tick.current = setInterval(() => setT((x) => x + 0.1), 100);
    return () => clearInterval(tick.current);
  }, [running, stage]);

  // Power-breath phase: alternate in/out, count breaths, then drop into retention.
  useEffect(() => {
    if (stage !== STAGES.BREATHS) return;
    const dur = inhaling ? pattern.breathIn : pattern.breathOut;
    if (t >= dur) {
      setT(0);
      if (inhaling) setInhaling(false);
      else {
        setInhaling(true);
        setBreathNum((n) => {
          if (n >= pattern.powerBreaths) {
            if (soundOn) playBeep(440, 250);
            setStage(STAGES.RETENTION);
            setT(0);
            return 1;
          }
          if (soundOn && n % 10 === 0) playBeep(700, 60);
          return n + 1;
        });
      }
    }
  }, [t, stage, inhaling, pattern, soundOn]);

  // Recovery hold is the only fixed-length hold, and it's short.
  useEffect(() => {
    if (stage !== STAGES.RECOVERY) return;
    if (t >= pattern.recoveryHold) {
      if (soundOn) playBeep(880, 200);
      if (round >= pattern.rounds) setStage(STAGES.DONE);
      else { setRound((r) => r + 1); setStage(STAGES.BREATHS); setBreathNum(1); setInhaling(true); }
      setT(0);
    }
  }, [t, stage, round, pattern, soundOn]);

  const endRetention = () => {
    setHoldTimes((h) => [...h, Math.round(t)]);
    if (soundOn) playBeep(660, 200);
    setStage(STAGES.RECOVERY);
    setT(0);
  };

  const fmtT = (x) => `${Math.floor(x / 60)}:${String(Math.floor(x % 60)).padStart(2, "0")}`;

  if (stage === STAGES.DONE) {
    return (
      <div className="pb-28 flex flex-col items-center px-6" style={{ minHeight: "70vh" }}>
        <TopBar title={pattern.name} onBack={onExit} accent={accent} />
        <p style={{ ...display, color: T.chalk, fontSize: 28 }} className="mt-6">Done</p>
        <p className="text-sm mt-2 mb-6" style={{ color: T.muted }}>{pattern.rounds} rounds complete</p>
        <div className="w-full space-y-2 mb-6">
          {holdTimes.map((h, i) => (
            <div key={i} className="flex justify-between p-3 rounded-xl" style={cardStyle()}>
              <span style={{ color: T.muted }}>Round {i + 1} hold</span>
              <span style={{ ...numeric, color: accent, fontSize: 19, fontWeight: 600 }}>{fmtT(h)}</span>
            </div>
          ))}
        </div>
        <button
          onClick={() => onLog({ type: "yoga", name: `${pattern.name} — breathwork`, detail: `${pattern.rounds} rounds · holds ${holdTimes.map(fmtT).join(", ")}` })}
          className="w-full py-3 rounded-xl" style={{ ...emberButton(accent), fontSize: 16 }}>
          Log this session
        </button>
        <button onClick={onExit} className="mt-3 text-sm" style={{ color: T.muted }}>Back to patterns</button>
      </div>
    );
  }

  const label =
    stage === STAGES.BREATHS ? (inhaling ? "Breathe in" : "Let it go")
    : stage === STAGES.RETENTION ? "Hold — empty lungs"
    : "Hold — full lungs";

  const scale =
    stage === STAGES.BREATHS ? (inhaling ? 0.5 + (t / pattern.breathIn) * 0.5 : 1 - (t / pattern.breathOut) * 0.5)
    : stage === STAGES.RETENTION ? 0.5
    : 1;

  return (
    <div className="pb-28 flex flex-col items-center" style={{ minHeight: "80vh" }}>
      <TopBar title={`${pattern.name} · Round ${round}/${pattern.rounds}`} onBack={onExit} accent={accent} />

      <div className="flex items-center justify-center" style={{ height: 250, width: 250 }}>
        <div style={{
          width: 210, height: 210, borderRadius: "9999px",
          background: accentDim, border: `2px solid ${accent}`,
          transform: `scale(${Math.max(0.4, Math.min(1.05, scale))})`,
          transition: "transform 120ms linear",
          display: "flex", alignItems: "center", justifyContent: "center",
        }}>
          <span style={{ ...numeric, color: accent, fontSize: 32, fontWeight: 600 }}>
            {stage === STAGES.BREATHS ? `${breathNum}/${pattern.powerBreaths}`
              : stage === STAGES.RETENTION ? fmtT(t)
              : Math.ceil(pattern.recoveryHold - t)}
          </span>
        </div>
      </div>

      <p style={{ ...display, color: T.chalk, fontSize: 26 }}>{label}</p>

      {stage === STAGES.RETENTION && (
        <>
          <p className="text-sm mt-2 text-center px-10" style={{ color: T.muted }}>
            No target. Breathe in again the moment you want to.
          </p>
          <button onClick={endRetention} className="mt-6 px-8 py-4 rounded-xl"
            style={{ background: accent, color: T.onAccent, ...display, fontSize: 17 }}>
            I need to breathe
          </button>
        </>
      )}

      {stage !== STAGES.RETENTION && (
        <div className="flex gap-4 mt-6">
          {running
            ? <button onClick={() => setRunning(false)} className="p-4 rounded-full" style={{ background: T.surface2 }}><Pause color={T.chalk} /></button>
            : <button onClick={() => setRunning(true)} className="p-4 rounded-full" style={{ background: accent }}><Play color={T.onAccent} /></button>}
          <button aria-label="Close" onClick={onExit} className="p-4 rounded-full" style={{ background: T.surface2 }}><X color={T.chalk} /></button>
        </div>
      )}

      <p className="text-xs text-center px-10 mt-8" style={{ color: T.muted }}>
        Sitting or lying down, never in water. Stop and breathe normally if you feel faint.
      </p>
    </div>
  );
}

/* Explicit acknowledgement before the retention protocol. Not a disclaimer to
   scroll past — the button stays disabled until each risk is ticked. */
function BreathSafetyGate({ pattern, accent, onAccept, onCancel }) {
  const RISKS = [
    "I'm sitting or lying down, not standing, driving, or in water",
    "I'll stop immediately if I feel faint, dizzy, or tingly",
    "I don't have a heart or respiratory condition, epilepsy, or a pregnancy that makes this unwise",
  ];
  const [checked, setChecked] = useState([false, false, false]);
  const allChecked = checked.every(Boolean);

  return (
    <div className="pb-28">
      <TopBar title="Before you start" onBack={onCancel} accent={accent} />
      <div className="px-4">
        <div className="rounded-2xl p-4 mb-4" style={{ background: T.emberDim, border: `1px solid ${T.ember}55` }}>
          <p style={{ ...display, color: T.emberInk, fontSize: 15 }} className="mb-2">This one carries real risk</p>
          <p className="text-sm" style={{ color: T.chalk }}>
            {pattern.rounds
              ? "Rapid breathing lowers carbon dioxide, which can make you faint with no warning."
              : "Holding an empty lung builds up carbon dioxide, and pushing it can make you faint with no warning."}
            {" "}People have drowned doing breath holds in water. Never practise this in or near water,
            in a bath, while driving, or standing up.
          </p>
        </div>

        <div className="space-y-2 mb-5">
          {RISKS.map((r, i) => (
            <button key={i} onClick={() => setChecked((c) => c.map((v, n) => (n === i ? !v : v)))}
              className="w-full flex items-start gap-3 p-3 rounded-xl text-left" style={{ background: T.surface }}>
              <span className="w-5 h-5 rounded shrink-0 mt-0.5 flex items-center justify-center"
                style={{ background: checked[i] ? accent : "transparent", border: `1.5px solid ${checked[i] ? accent : T.line}` }}>
                {checked[i] && <Check size={13} color={T.onAccent} />}
              </span>
              <span className="text-sm" style={{ color: T.chalk }}>{r}</span>
            </button>
          ))}
        </div>

        <button onClick={onAccept} disabled={!allChecked}
          className="w-full py-3 rounded-xl" style={{ background: allChecked ? accent : T.surface2, color: allChecked ? T.ink : T.muted, ...display, fontSize: 16 }}>
          {allChecked ? "Start" : "Tick all three to continue"}
        </button>
        <button onClick={onCancel} className="w-full mt-3 text-sm" style={{ color: T.muted }}>Pick a gentler pattern instead</button>
      </div>
    </div>
  );
}

/* ============================== BREATH GUIDES ==============================
   Four ways of drawing the same thing, because the right guide depends on the
   pattern. A circle can't show you where the corners of box breathing are, and
   a bar can't show you a hold at all. Each takes the same inputs: which phase,
   how far through it, and whether that phase is an inhale, hold, or exhale.

   Everything is driven by `progress` rather than CSS animation, so pausing
   genuinely freezes the guide instead of letting it drift out of sync.
==================================================================== */
/* Phase names are written for the athlete, not for a regex — "Hum out" and
   "Sniff" are breaths even though neither word says inhale or exhale. Getting
   this wrong draws a hold where the lungs are actually moving. */
function phaseKind(name) {
  const n = String(name).toLowerCase();
  if (/inhale|breathe in|sniff|inhale left|inhale right/.test(n)) return "in";
  if (/exhale|release|sharp|hum|sigh|let it go|out\b/.test(n)) return "out";
  return "hold";
}

const GUIDE_STYLES = [
  { id: "orb", name: "Orb", blurb: "Expands and contracts with the breath" },
  { id: "path", name: "Path", blurb: "Traces the shape of the pattern" },
  { id: "wave", name: "Wave", blurb: "A line that rises and falls" },
  { id: "bars", name: "Column", blurb: "Fills like lungs" },
];

/* 1 — Orb. Concentric rings so the movement is legible even at a glance,
   with a soft halo that swells on the inhale. */
function GuideOrb({ progress, kind, accent, count, label }) {
  const scale = kind === "in" ? 0.5 + progress * 0.5
    : kind === "out" ? 1.0 - progress * 0.5
    : 1.0;
  const glow = kind === "hold" ? 0.5 : scale - 0.4;
  return (
    <div className="relative flex items-center justify-center" style={{ width: 250, height: 250 }}>
      <div style={{
        position: "absolute", width: 230, height: 230, borderRadius: "9999px",
        background: accent, opacity: 0.1 * glow,
        transform: `scale(${scale * 1.05})`, transition: "transform 110ms linear",
      }} />
      {[1, 0.78, 0.56].map((r, i) => (
        <div key={i} style={{
          position: "absolute", width: 210 * r, height: 210 * r, borderRadius: "9999px",
          border: `${i === 0 ? 2 : 1}px solid ${accent}`,
          opacity: i === 0 ? 0.9 : 0.28 - i * 0.06,
          transform: `scale(${scale})`, transition: "transform 110ms linear",
        }} />
      ))}
      <div className="text-center" style={{ zIndex: 1 }}>
        <p style={{ ...numeric, color: accent, fontSize: 40, fontWeight: 700, lineHeight: 1 }}>{count}</p>
        <p style={{ ...display, color: T.muted, fontSize: 11, letterSpacing: "0.14em" }}>{label}</p>
      </div>
    </div>
  );
}

/* 2 — Path. Walks a dot around a shape whose corners ARE the phase changes:
   a square for four-phase patterns, a triangle for three, a line for two.
   The geometry teaches the pattern without needing to read the numbers. */
function GuidePath({ phases, phaseIdx, progress, accent, count, label }) {
  const n = phases.length;
  const size = 210, pad = 26, box = size - pad * 2;

  let pts;
  if (n >= 4) pts = [[pad, size - pad], [pad, pad], [size - pad, pad], [size - pad, size - pad]];
  else if (n === 3) pts = [[size / 2, pad], [size - pad, size - pad], [pad, size - pad]];
  else pts = [[pad, size - pad], [size - pad, pad]];

  const seg = phaseIdx % pts.length;
  const a = pts[seg];
  const b = pts[(seg + 1) % pts.length];
  const x = a[0] + (b[0] - a[0]) * progress;
  const y = a[1] + (b[1] - a[1]) * progress;
  const d = n >= 3
    ? pts.map((p, i) => `${i ? "L" : "M"}${p[0]},${p[1]}`).join(" ") + " Z"
    : `M${pts[0][0]},${pts[0][1]} L${pts[1][0]},${pts[1][1]}`;

  return (
    <div className="relative flex items-center justify-center" style={{ width: 250, height: 250 }}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} style={{ overflow: "visible" }}>
        <path d={d} fill="none" stroke={accent} strokeWidth="2" opacity="0.18" strokeLinejoin="round" />
        <path d={`M${a[0]},${a[1]} L${x},${y}`} fill="none" stroke={accent} strokeWidth="3.5" strokeLinecap="round" />
        {pts.map((p, i) => (
          <circle key={i} cx={p[0]} cy={p[1]} r={i === seg ? 5 : 3.5} fill={accent} opacity={i === seg ? 1 : 0.3} />
        ))}
        <circle cx={x} cy={y} r="9" fill={accent} />
        <circle cx={x} cy={y} r="16" fill={accent} opacity="0.18" />
      </svg>
      <div className="absolute text-center">
        <p style={{ ...numeric, color: accent, fontSize: 34, fontWeight: 700, lineHeight: 1 }}>{count}</p>
        <p style={{ ...display, color: T.muted, fontSize: 10, letterSpacing: "0.14em" }}>{label}</p>
      </div>
    </div>
  );
}

/* 3 — Wave. Draws the whole cycle as a continuous curve with a marker riding
   along it, so you can see what's coming rather than only where you are. */
function GuideWave({ phases, phaseIdx, progress, accent, count, label }) {
  const W = 260, H = 150, base = H - 22, top = 22;
  const total = phases.reduce((sum, p) => sum + p[1], 0);

  // Build the cycle's silhouette once: inhales rise, exhales fall, holds run flat.
  let t = 0, level = 0;
  const pts = [[0, base]];
  phases.forEach(([nm, sec]) => {
    const k = phaseKind(nm);
    t += sec;
    if (k === "in") level = 1;
    if (k === "out") level = 0;
    pts.push([(t / total) * W, base - level * (base - top)]);
  });

  const elapsedBefore = phases.slice(0, phaseIdx).reduce((sum, p) => sum + p[1], 0);
  const nowT = elapsedBefore + phases[phaseIdx][1] * progress;
  const cx = (nowT / total) * W;

  // Sample the silhouette at the marker's x so the dot sits on the line.
  let cy = base;
  for (let i = 1; i < pts.length; i++) {
    if (cx <= pts[i][0]) {
      const [x0, y0] = pts[i - 1], [x1, y1] = pts[i];
      const r = x1 === x0 ? 0 : (cx - x0) / (x1 - x0);
      cy = y0 + (y1 - y0) * r;
      break;
    }
  }
  const d = pts.map((p, i) => `${i ? "L" : "M"}${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(" ");

  return (
    <div className="flex items-center justify-center" style={{ width: 270, height: 250 }}>
      <svg width={W} height={H} viewBox={`0 0 ${W} ${H}`} style={{ overflow: "visible" }}>
        <path d={`${d} L${W},${base} L0,${base} Z`} fill={accent} opacity="0.1" />
        <path d={d} fill="none" stroke={accent} strokeWidth="2.5" strokeLinejoin="round" strokeLinecap="round" opacity="0.55" />
        <line x1={cx} y1={top - 8} x2={cx} y2={base + 8} stroke={accent} strokeWidth="1" opacity="0.28" />
        <circle cx={cx} cy={cy} r="8" fill={accent} />
        <circle cx={cx} cy={cy} r="15" fill={accent} opacity="0.18" />
        <text x={W / 2} y={H + 34} textAnchor="middle" style={{ ...numeric, fontSize: 30, fontWeight: 700 }} fill={accent}>{count}</text>
        <text x={W / 2} y={H + 50} textAnchor="middle" style={{ ...display, fontSize: 10, letterSpacing: "0.14em" }} fill={T.muted}>{label}</text>
      </svg>
    </div>
  );
}

/* 4 — Column. A vertical fill, closest to what lungs actually do, and the
   easiest to follow with your eyes half shut. */
function GuideBars({ progress, kind, accent, count, label }) {
  const fill = kind === "in" ? progress : kind === "out" ? 1 - progress : 1;
  const held = kind === "hold";
  return (
    <div className="flex items-center justify-center gap-5" style={{ width: 250, height: 250 }}>
      <div style={{
        width: 74, height: 200, borderRadius: 22, background: T.surface2,
        border: `1px solid ${T.hairline}`, position: "relative", overflow: "hidden",
      }}>
        <div style={{
          position: "absolute", bottom: 0, left: 0, right: 0,
          height: `${Math.max(4, fill * 100)}%`,
          background: accent, opacity: held ? 0.72 : 1,
          borderRadius: "18px 18px 20px 20px",
          transition: "height 110ms linear",
        }} />
        {held && (
          <div style={{
            position: "absolute", inset: 0, border: `2.5px solid ${accent}`,
            borderRadius: 22, opacity: 0.85,
          }} />
        )}
      </div>
      <div>
        <p style={{ ...numeric, color: accent, fontSize: 44, fontWeight: 700, lineHeight: 1 }}>{count}</p>
        <p style={{ ...display, color: T.muted, fontSize: 11, letterSpacing: "0.14em" }}>{label}</p>
      </div>
    </div>
  );
}

function BreathGuide({ style, phases, phaseIdx, progress, accent, count, label }) {
  const kind = phaseKind(phases[phaseIdx][0]);
  const common = { progress, kind, accent, count, label };
  if (style === "path") return <GuidePath phases={phases} phaseIdx={phaseIdx} {...common} />;
  if (style === "wave") return <GuideWave phases={phases} phaseIdx={phaseIdx} {...common} />;
  if (style === "bars") return <GuideBars {...common} />;
  return <GuideOrb {...common} />;
}

function BreathScreen({ store, onLog, jumpPattern, clearJumpPattern }) {
  const accent = "#6FB6C9";
  const accentDim = "#16262B";
  const { settings, acks, acknowledge } = store;

  const [pattern, setPattern] = useState(null);
  const [guide, setGuide] = useState("orb");
  const [gating, setGating] = useState(null);   // pattern awaiting acknowledgement
  const [running, setRunning] = useState(false);
  const [phaseIdx, setPhaseIdx] = useState(0);
  const [remaining, setRemaining] = useState(0);
  const [cycles, setCycles] = useState(0);
  const [elapsed, setElapsed] = useState(0);
  const tickRef = useRef(null);

  const phases = pattern?.phases || [];
  const [phaseName, phaseLen] = phases[phaseIdx] || ["", 0];

  // A 100ms tick keeps the visual smooth while staying cheap.
  useEffect(() => {
    if (!running || !pattern) return;
    tickRef.current = setInterval(() => {
      setElapsed((e) => e + 0.1);
      setRemaining((r) => {
        const next = r - 0.1;
        if (next > 0.05) return next;
        setPhaseIdx((pi) => {
          const nextIdx = (pi + 1) % phases.length;
          if (nextIdx === 0) setCycles((c) => c + 1);
          if (settings.sound) {
            const [nm, sec] = phases[nextIdx];
            const k = phaseKind(nm);
            // The tone glides with the breath, so the sound itself tells you
            // which way to go — no need to read the screen.
            if (k === "in") Sound.inhale(sec);
            else if (k === "out") Sound.exhale(sec);
            else Sound.hold();
            if (nextIdx === 0) Sound.cycleDone();
          }
          if (settings.voice) Sound.say(phases[nextIdx][0], true);
          setRemaining(phases[nextIdx][1]);
          return nextIdx;
        });
        return phases[(phaseIdx + 1) % phases.length][1];
      });
    }, 100);
    return () => clearInterval(tickRef.current);
  }, [running, pattern, phaseIdx, phases, settings.sound]);

  const start = (p) => {
    if (p.requiresAck && !acks["breath:" + p.id]) { setGating(p); return; }
    if (settings.sound) {
      const [nm, sec] = p.phases[0];
      const k = phaseKind(nm);
      if (k === "in") Sound.inhale(sec); else if (k === "out") Sound.exhale(sec); else Sound.hold();
    }
    if (settings.voice) Sound.say(p.phases[0][0], true);
    // Default to the guide that suits the pattern's shape: a path for anything
    // with corners, a wave for long multi-stage cycles, an orb for simple ones.
    const n = p.phases?.length || 0;
    setGuide(n >= 5 ? "wave" : n >= 3 ? "path" : "orb");
    setPattern(p); setPhaseIdx(0); setRemaining(p.phases[0][1]);
    setCycles(0); setElapsed(0); setRunning(true);
  };
  const stop = () => { setRunning(false); clearInterval(tickRef.current); };
  const reset = () => { stop(); Sound.stopSpeech(); setPattern(null); };

  // Arriving from "Build it" with a pattern already chosen starts it, through
  // start() so the safety gate on breath-hold patterns still applies.
  useEffect(() => {
    if (!jumpPattern) return;
    const p = BREATH_PATTERNS.find((x) => x.id === jumpPattern);
    clearJumpPattern?.();
    if (p) start(p);
    // eslint-disable-next-line
  }, [jumpPattern]);

  if (gating) {
    return (
      <BreathSafetyGate
        pattern={gating}
        accent={accent}
        onCancel={() => setGating(null)}
        onAccept={() => { acknowledge("breath:" + gating.id); setPattern(gating); setGating(null); }}
      />
    );
  }

  if (pattern?.rounds) {
    return (
      <WimHofRunner
        pattern={pattern}
        accent={accent}
        accentDim={accentDim}
        soundOn={settings.sound}
        onExit={() => setPattern(null)}
        onLog={onLog}
      />
    );
  }

  if (pattern) {
    const progress = phaseLen ? 1 - remaining / phaseLen : 0;

    return (
      <div className="pb-28 flex flex-col items-center" style={{ minHeight: "80vh" }}>
        <TopBar title={pattern.name} onBack={reset} accent={accent} />

        <BreathGuide
          style={guide}
          phases={phases}
          phaseIdx={phaseIdx}
          progress={progress}
          accent={accent}
          count={Math.ceil(remaining)}
          label={phaseName}
        />

        <p className="text-sm mt-1" style={{ color: T.muted }}>
          {cycles} {cycles === 1 ? "breath" : "breaths"} · {Math.floor(elapsed / 60)}:{String(Math.floor(elapsed % 60)).padStart(2, "0")}
        </p>

        {/* Guide style is a live preference — swapping mid-session shouldn't
            interrupt the breathing, so it never touches the timer. */}
        <div className="flex gap-1.5 mt-4">
          {GUIDE_STYLES.map((g) => (
            <button key={g.id} onClick={() => setGuide(g.id)} className="px-3 py-1.5 rounded-full text-[11px]"
              style={{
                background: guide === g.id ? accent : T.surface,
                color: guide === g.id ? T.onAccent : T.chalkDim,
                border: `1px solid ${guide === g.id ? accent : T.line}`,
                fontWeight: guide === g.id ? 700 : 500,
              }}>
              {g.name}
            </button>
          ))}
        </div>

        <div className="flex gap-4 mt-6">
          {running
            ? <button onClick={stop} className="p-4 rounded-full" style={{ background: T.surface2 }} aria-label="Pause"><Pause color={T.chalk} /></button>
            : <button onClick={() => setRunning(true)} className="p-4 rounded-full" style={{ background: accent }} aria-label="Resume"><Play color={T.onAccent} /></button>}
          <button onClick={reset} className="p-4 rounded-full" style={{ background: T.surface2 }} aria-label="Reset"><RotateCcw color={T.chalk} /></button>
          {cycles > 0 && (
            <button
              onClick={() => onLog({ type: "yoga", name: `${pattern.name} — breathwork`, detail: `${cycles} breaths` })}
              className="px-5 py-4 rounded-full text-sm" style={{ background: T.surface2, color: T.chalk }}>
              Log
            </button>
          )}
        </div>

        <p className="text-xs text-center px-10 mt-7" style={{ color: T.muted }}>
          Breathe gently. If you feel lightheaded or dizzy, stop and return to normal breathing.
        </p>
      </div>
    );
  }

  return (
    <div className="pb-28">
      <TopBar title="Breath Trainer" accent={accent} />
      <div className="px-4">
        <p className="text-sm mb-4" style={{ color: T.muted }}>
          Paced breathing for focus before training and recovery after it. Follow the circle — it grows as you breathe in and shrinks as you breathe out.
        </p>
        {/* Grouped by what you'd reach for it — fifteen patterns in a flat
            list is a wall, but four short sections you can scan is not. */}
        {["Focus", "Recovery", "Wind down", "Training"].map((grp) => {
          const inGroup = BREATH_PATTERNS.filter((p) => p.group === grp);
          if (!inGroup.length) return null;
          return (
            <div key={grp} className="mb-5">
              <SectionLabel>{grp.toUpperCase()}</SectionLabel>
              <div className="space-y-2">
                {inGroup.map((p) => (
                  <button key={p.id} onClick={() => start(p)} className="w-full text-left p-4 rounded-xl"
                    style={cardStyle(p.requiresAck ? T.ember : T.breath)}>
                    <div className="flex justify-between items-baseline">
                      <p style={{ color: T.chalk, fontWeight: 700, fontSize: 15 }}>{p.name}</p>
                      <span className="text-[10px] px-2 py-0.5 rounded-full"
                        style={{ background: p.requiresAck ? T.emberDim : T.breathDim, color: p.requiresAck ? T.emberInk : T.breathInk }}>
                        {p.tag}
                      </span>
                    </div>
                    <p style={{ ...numeric, color: p.requiresAck ? T.emberInk : T.breathInk, fontSize: 11.5 }} className="mt-1">
                      {p.rounds
                        ? `${p.rounds} rounds · ${p.powerBreaths} breaths · hold · ${p.recoveryHold}s recovery`
                        : p.phases.map(([n, sec]) => `${n} ${sec}s`).join(" · ")}
                    </p>
                    <p className="text-[11.5px] mt-1.5" style={{ color: T.muted, lineHeight: 1.45 }}>{p.about}</p>
                  </button>
                ))}
              </div>
            </div>
          );
        })}

        <div className="rounded-xl p-3" style={{ background: T.surface2 }}>
          <p className="text-xs" style={{ color: T.muted }}>
            Most patterns here are slow and continuous. The Wim Hof Method is different — it uses
            rapid breathing and breath holds, which can cause fainting without warning. Never do it
            in or near water, in a bath, while driving, or standing up. If you have a heart or
            respiratory condition, epilepsy, or you're pregnant, talk to a clinician first.
          </p>
        </div>
      </div>
    </div>
  );
}

/* ============================== FAVORITES ============================== */
function FavoritesScreen({ store, onOpenExercise, onOpenYogaArea, onLoadWod }) {
  const { favorites, toggleFavorite, savedWods, toggleSavedWod } = store;
  const [tab, setTab] = useState("wods");

  const favEx = EX.filter((e) => favorites.includes("ex:" + e.id));
  const favAreas = Object.keys(YOGA_AREAS).filter((a) => favorites.includes("yoga:" + a));
  const counts = { wods: savedWods.length, moves: favEx.length, yoga: favAreas.length };
  const empty = counts.wods + counts.moves + counts.yoga === 0;

  return (
    <div className="pb-28">
      <TopBar title="Favorites" accent={T.amber} />
      <div className="px-4">
        {empty ? (
          <p className="text-sm text-center mt-12" style={{ color: T.muted }}>
            Nothing saved yet. Tap the star on any workout, movement, or yoga flow and it lands here.
          </p>
        ) : (
          <>
            <div className="flex mb-4">
              {[["wods", "Workouts"], ["moves", "Movements"], ["yoga", "Yoga"]].map(([id, label]) => (
                <button key={id} onClick={() => setTab(id)} className="flex-1 py-2 rounded-full text-xs mr-2"
                  style={{ background: tab === id ? T.amber : T.surface2, color: tab === id ? T.onAccent : T.muted, fontWeight: tab === id ? 700 : 500 }}>
                  {label}{counts[id] > 0 ? ` (${counts[id]})` : ""}
                </button>
              ))}
            </div>

            {tab === "wods" && (
              savedWods.length === 0
                ? <p className="text-sm text-center mt-8" style={{ color: T.muted }}>No saved workouts yet.</p>
                : <div className="space-y-2">
                    {savedWods.map((w) => (
                      <div key={w.key} className="p-3 rounded-xl" style={cardStyle()}>
                        <div className="flex justify-between items-start mb-1">
                          <div className="flex-1">
                            <p style={{ ...display, color: T.chalk, fontSize: 17 }}>{w.title}</p>
                            <p className="text-xs" style={{ color: T.muted }}>{w.meta}</p>
                          </div>
                          <button onClick={() => toggleSavedWod(w)}><Star size={17} color={T.amber} fill={T.amber} /></button>
                        </div>
                        <p className="text-xs mb-2" style={{ color: T.muted }}>
                          {(w.movements || []).map((m) => m.name).join(" · ")}
                        </p>
                        <button onClick={() => onLoadWod(w)} className="w-full py-2 rounded-lg flex items-center justify-center gap-2 text-sm"
                          style={{ background: T.surface2, color: T.chalk }}>
                          <Play size={15} /> Run it again
                        </button>
                      </div>
                    ))}
                  </div>
            )}

            {tab === "moves" && (
              favEx.length === 0
                ? <p className="text-sm text-center mt-8" style={{ color: T.muted }}>No saved movements yet.</p>
                : <div className="space-y-2">
                    {favEx.map((e) => (
                      <div key={e.id} className="flex items-center gap-3 p-3 rounded-xl" style={cardStyle()}>
                        <IconBadge pattern={e.pattern} color={T.ember} bg={T.surface2} size={44} />
                        <button onClick={() => onOpenExercise(e)} className="flex-1 text-left">
                          <p style={{ color: T.chalk, fontWeight: 600 }}>{e.name}</p>
                          <p className="text-xs" style={{ color: T.muted }}>{e.muscle.join(" · ")}</p>
                        </button>
                        <button onClick={() => toggleFavorite("ex:" + e.id)}><Star size={17} color={T.amber} fill={T.amber} /></button>
                      </div>
                    ))}
                  </div>
            )}

            {tab === "yoga" && (
              favAreas.length === 0
                ? <p className="text-sm text-center mt-8" style={{ color: T.muted }}>No saved yoga flows yet.</p>
                : <div className="space-y-2">
                    {favAreas.map((a) => (
                      <div key={a} className="flex items-center gap-3 p-3 rounded-xl" style={cardStyle()}>
                        <IconBadge pose="yoga" color={T.sage} bg={T.surface2} size={44} />
                        <button onClick={() => onOpenYogaArea(a)} className="flex-1 text-left">
                          <p style={{ color: T.chalk, fontWeight: 600 }}>{a}</p>
                          <p className="text-xs" style={{ color: T.muted }}>{YOGA_AREAS[a].length} poses</p>
                        </button>
                        <button onClick={() => toggleFavorite("yoga:" + a)}><Star size={17} color={T.amber} fill={T.amber} /></button>
                      </div>
                    ))}
                  </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}

/* ============================== SEARCH ============================== */
function SearchScreen({ store, onOpenExercise, onOpenPose }) {
  const [q, setQ] = useState("");
  const query = q.trim().toLowerCase();
  const exMatches = query ? EX.filter((e) => e.name.toLowerCase().includes(query) || e.muscle.some((m) => m.toLowerCase().includes(query))) : [];
  const poseMatches = query
    ? Object.entries(POSE_LIB).filter(([, p]) => p.name.toLowerCase().includes(query))
    : [];
  const favEx = EX.filter((e) => store.favorites.includes("ex:" + e.id));

  return (
    <div className="pb-28">
      <TopBar title="Search" />
      <div className="px-4">
        <input
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder="Search movements and poses"
          className="w-full p-3 rounded-xl mb-4"
          style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }}
        />

        {!query && favEx.length > 0 && (
          <>
            <p style={{ ...display, color: T.muted, fontSize: 13 }} className="mb-2">Your Favorites</p>
            <div className="space-y-2 mb-4">
              {favEx.map((e) => (
                <button key={e.id} onClick={() => onOpenExercise(e)} className="w-full flex items-center gap-3 p-3 rounded-xl" style={cardStyle()}>
                  <IconBadge pattern={e.pattern} color={T.ember} bg={T.surface2} size={44} />
                  <span className="flex-1 text-left" style={{ color: T.chalk, fontWeight: 600 }}>{e.name}</span>
                  <ChevronRight color={T.muted} size={18} />
                </button>
              ))}
            </div>
          </>
        )}

        {!query && favEx.length === 0 && (
          <p className="text-sm text-center mt-10" style={{ color: T.muted }}>
            Search all {EX.length} movements and {Object.keys(POSE_LIB).length} yoga poses. Star anything to pin it here.
          </p>
        )}

        {query && exMatches.length === 0 && poseMatches.length === 0 && (
          <p className="text-sm text-center mt-10" style={{ color: T.muted }}>No matches. Try a muscle group, like "glutes".</p>
        )}

        {exMatches.length > 0 && (
          <>
            <p style={{ ...display, color: T.muted, fontSize: 13 }} className="mb-2">Movements</p>
            <div className="space-y-2 mb-4">
              {exMatches.slice(0, 30).map((e) => (
                <button key={e.id} onClick={() => onOpenExercise(e)} className="w-full flex items-center gap-3 p-3 rounded-xl" style={cardStyle()}>
                  <IconBadge pattern={e.pattern} color={T.ember} bg={T.surface2} size={44} />
                  <div className="flex-1 text-left">
                    <p style={{ color: T.chalk, fontWeight: 600 }}>{e.name}</p>
                    <p className="text-xs" style={{ color: T.muted }}>{e.muscle.join(" · ")}</p>
                  </div>
                  <ChevronRight color={T.muted} size={18} />
                </button>
              ))}
            </div>
          </>
        )}

        {poseMatches.length > 0 && (
          <>
            <p style={{ ...display, color: T.muted, fontSize: 13 }} className="mb-2">Yoga Poses</p>
            <div className="space-y-2">
              {poseMatches.slice(0, 20).map(([key, p]) => (
                <button key={key} onClick={() => onOpenPose(key)} className="w-full flex items-center gap-3 p-3 rounded-xl" style={cardStyle()}>
                  <IconBadge pose={p.diagram} color={T.sage} bg={T.surface2} size={44} />
                  <div className="flex-1 text-left">
                    <p style={{ color: T.chalk, fontWeight: 600 }}>{p.name}</p>
                    <p className="text-xs" style={{ color: T.muted }}>{p.hold}s hold</p>
                  </div>
                </button>
              ))}
            </div>
          </>
        )}
      </div>
    </div>
  );
}

/* ============================== CUSTOM WOD BUILDER ============================== */
function BuilderScreen({ store, onBack, onLaunch }) {
  const [title, setTitle] = useState("My Workout");
  const [format, setFormat] = useState("AMRAP");
  const [minutes, setMinutes] = useState(15);
  const [items, setItems] = useState([]);
  const [picker, setPicker] = useState(false);
  const [q, setQ] = useState("");

  const add = (ex) => {
    setItems((i) => [...i, { name: ex.name, pattern: ex.pattern, prop: ex.prop, scale: ex.scale, reps: ex.unit === "reps" ? "10" : ex.unit === "cal" ? "12 cal" : "200m" }]);
    setPicker(false);
    setQ("");
  };
  const update = (idx, reps) => setItems((i) => i.map((it, n) => (n === idx ? { ...it, reps } : it)));
  const remove = (idx) => setItems((i) => i.filter((_, n) => n !== idx));

  if (picker) {
    const matches = EX.filter((e) => e.name.toLowerCase().includes(q.trim().toLowerCase())).slice(0, 40);
    return (
      <div className="pb-28">
        <TopBar title="Add Movement" onBack={() => setPicker(false)} />
        <div className="px-4">
          <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search movements" className="w-full p-3 rounded-xl mb-4" style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }} />
          <div className="space-y-2">
            {matches.map((e) => (
              <button key={e.id} onClick={() => add(e)} className="w-full flex items-center gap-3 p-3 rounded-xl" style={cardStyle()}>
                <IconBadge pattern={e.pattern} color={T.ember} bg={T.surface2} size={44} />
                <span className="flex-1 text-left" style={{ color: T.chalk, fontWeight: 600 }}>{e.name}</span>
              </button>
            ))}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="pb-28">
      <TopBar title="Build a WOD" onBack={onBack} />
      <div className="px-4">
        <input value={title} onChange={(e) => setTitle(e.target.value)} className="w-full p-3 rounded-xl mb-3" style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }} />
        <div className="flex flex-wrap mb-3">
          {["AMRAP", "RFT", "EMOM", "Chipper", "Tabata"].map((f) => (
            <Chip key={f} active={format === f} onClick={() => setFormat(f)}>{f}</Chip>
          ))}
        </div>
        {(format === "AMRAP" || format === "EMOM") && (
          <div className="flex items-center gap-3 mb-4">
            <button onClick={() => setMinutes((m) => Math.max(1, m - 1))} className="w-9 h-9 rounded-full" style={{ background: T.surface2, color: T.chalk }}>-</button>
            <span style={{ color: T.chalk }}>{minutes} minutes</span>
            <button onClick={() => setMinutes((m) => m + 1)} className="w-9 h-9 rounded-full" style={{ background: T.surface2, color: T.chalk }}>+</button>
          </div>
        )}

        <div className="space-y-2 mb-3">
          {items.map((it, i) => (
            <div key={i} className="flex items-center gap-2 p-3 rounded-xl" style={{ background: T.surface }}>
              <IconBadge pattern={it.pattern} color={T.ember} bg={T.surface2} size={40} />
              <input value={it.reps} onChange={(e) => update(i, e.target.value)} className="w-20 p-2 rounded-lg text-sm" style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }} />
              <span className="flex-1 text-sm" style={{ color: T.chalk }}>{it.name}</span>
              <button onClick={() => remove(i)}><X size={16} color={T.muted} /></button>
            </div>
          ))}
        </div>

        <button onClick={() => setPicker(true)} className="w-full py-3 rounded-xl mb-3" style={{ background: T.surface2, color: T.chalk }}>+ Add movement</button>
        {items.length > 0 && (
          <button
            onClick={() => onLaunch({
              title, format,
              meta: format === "AMRAP" ? `${minutes} minute AMRAP` : format === "EMOM" ? `EMOM ${minutes} minutes` : format === "RFT" ? "For time" : format,
              difficulty: "Custom",
              movements: items,
              timerCfg: format === "Tabata" ? { work: 20, rest: 10, rounds: 8 * items.length } : { total: minutes * 60 },
            })}
            className="w-full py-3 rounded-xl flex items-center justify-center gap-2"
            style={{ ...emberButton(), fontSize: 16 }}
          >
            <Play size={18} /> Load this WOD
          </button>
        )}
      </div>
    </div>
  );
}

/* ============================== HISTORY & PRs ============================== */
function ProgressScreen({ store, onOpenMetrics }) {
  const [tab, setTab] = useState("trends");
  const [editing, setEditing] = useState(null);
  const { history, prs, settings, metrics } = store;
  const accent = T.amber;

  const streak = useMemo(() => {
    if (!history.length) return 0;
    const days = new Set(history.map((h) => h.date.slice(0, 10)));
    let n = 0;
    const d = new Date();
    // Today not yet logged shouldn't break a streak that's alive through yesterday.
    if (!days.has(d.toISOString().slice(0, 10))) d.setDate(d.getDate() - 1);
    while (days.has(d.toISOString().slice(0, 10))) { n++; d.setDate(d.getDate() - 1); }
    return n;
  }, [history]);

  // Sessions per week over the last 8 weeks, oldest first.
  const weeklyVolume = useMemo(() => {
    const weeks = [];
    for (let w = 7; w >= 0; w--) {
      const end = new Date(); end.setHours(23, 59, 59, 999); end.setDate(end.getDate() - w * 7);
      const start = new Date(end); start.setDate(start.getDate() - 6); start.setHours(0, 0, 0, 0);
      weeks.push({ x: 7 - w, y: history.filter((h) => { const d = new Date(h.date); return d >= start && d <= end; }).length });
    }
    return weeks;
  }, [history]);

  const muscleBalance = useMemo(() => {
    const counts = {};
    MUSCLE_GROUPS.forEach((m) => (counts[m] = 0));
    history.slice(0, 60).forEach((h) => {
      const ex = EX.find((e) => e.name === h.name);
      if (ex) ex.muscle.forEach((m) => { if (counts[m] !== undefined) counts[m]++; });
    });
    return MUSCLE_GROUPS.map((m) => ({ label: m, value: counts[m] }));
  }, [history]);

  const weightSeries = useMemo(
    () => metrics.filter((m) => m.weightKg).slice(0, 30).reverse()
      .map((m, i) => ({ x: i, y: settings.units === "lb" ? m.weightKg * KG_TO_LB : m.weightKg })),
    [metrics, settings.units]
  );

  const rpeSeries = useMemo(
    () => history.filter((h) => h.rpe).slice(0, 20).reverse().map((h, i) => ({ x: i, y: h.rpe })),
    [history]
  );

  const latest = metrics[0];

  const exportCsv = () => downloadFile(`fitforge-history-${new Date().toISOString().slice(0, 10)}.csv`, historyToCsv(history, settings.units), "text/csv");
  const exportMetrics = () => downloadFile(`fitforge-metrics-${new Date().toISOString().slice(0, 10)}.csv`, metricsToCsv(metrics, settings.units), "text/csv");
  const exportBackup = () => downloadFile(`fitforge-backup-${new Date().toISOString().slice(0, 10)}.json`, buildBackup(store), "application/json");

  return (
    <div className="pb-28">
      <TopBar title="Progress" accent={accent}
        right={<button aria-label="Add" onClick={onOpenMetrics} className="text-xs px-3 py-1.5 rounded-full flex items-center gap-1" style={{ background: T.surface2, color: T.chalk }}>
          <Plus size={13} /> Check in
        </button>} />
      <div className="px-4">
        <div className="grid grid-cols-4 gap-2 mb-4">
          {[
            [history.length, "Sessions"],
            [streak, "Day streak"],
            [Object.keys(prs).length, "PRs"],
            [latest?.weightKg ? displayWeight(latest.weightKg, settings.units).split(" ")[0] : "—", settings.units === "lb" ? "Weight lb" : "Weight kg"],
          ].map(([v, l]) => (
            <div key={l} className="rounded-xl p-2.5" style={cardStyle()}>
              <Stat value={v} label={l} size={22} />
            </div>
          ))}
        </div>

        <div className="flex mb-4">
          {[["trends", "Trends"], ["history", "History"], ["prs", "PRs"], ["data", "Data"]].map(([id, label]) => (
            <button key={id} onClick={() => setTab(id)} className="flex-1 py-2 rounded-full text-xs mr-2"
              style={{ background: tab === id ? accent : T.surface2, color: tab === id ? T.onAccent : T.muted, fontWeight: tab === id ? 700 : 500 }}>
              {label}
            </button>
          ))}
        </div>

        {tab === "trends" && (
          <div className="space-y-4">
            <div className="rounded-2xl p-4" style={cardStyle()}>
              <p style={{ ...display, color: T.chalk, fontSize: 14 }} className="mb-2">Sessions per week</p>
              <LineChart points={weeklyVolume} color={accent} label="last 8 weeks" />
            </div>
            <div className="rounded-2xl p-4" style={cardStyle()}>
              <p style={{ ...display, color: T.chalk, fontSize: 14 }} className="mb-3">Muscle group balance</p>
              <MuscleHeatMap counts={Object.fromEntries(muscleBalance.map((b) => [b.label, b.value]))} />
              <p className="text-[10px] mt-3" style={{ color: T.muted }}>
                Across your last 60 sessions. Hot areas are getting the most work.
              </p>
            </div>
            {weightSeries.length > 1 && (
              <div className="rounded-2xl p-4" style={cardStyle()}>
                <p style={{ ...display, color: T.chalk, fontSize: 14 }} className="mb-2">Bodyweight</p>
                <LineChart points={weightSeries} color={T.sage} label={settings.units} formatY={(v) => Math.round(v)} />
              </div>
            )}
            {rpeSeries.length > 1 && (
              <div className="rounded-2xl p-4" style={cardStyle()}>
                <p style={{ ...display, color: T.chalk, fontSize: 14 }} className="mb-2">Effort over time</p>
                <LineChart points={rpeSeries} color={PLATE[1]} label="last 20 rated sessions (1-10)" />
              </div>
            )}
            {history.length === 0 && (
              <p className="text-sm text-center mt-8" style={{ color: T.muted }}>Log a few sessions and your trends will show up here.</p>
            )}
          </div>
        )}

        {tab === "history" && (
          history.length === 0 ? (
            <p className="text-sm text-center mt-10" style={{ color: T.muted }}>Nothing logged yet. Finish a workout and tap Log to start building your history.</p>
          ) : (
            <div className="space-y-2">
              {history.slice(0, 60).map((h) => (
                <button key={h.id} onClick={() => setEditing(h)} className="w-full text-left p-3 rounded-xl flex gap-3" style={cardStyle()}>
                  <div className="shrink-0 pt-0.5"><LoadRing rpe={h.rpe} size={40} /></div>
                  <div className="flex-1 min-w-0">
                    <div className="flex justify-between items-start">
                      <p style={{ color: T.chalk, fontWeight: 600 }}>{h.name}</p>
                      <span className="text-[11px] flex items-center gap-1 shrink-0" style={{ color: T.muted }}>
                        {new Date(h.date).toLocaleDateString()} <Pencil size={11} />
                      </span>
                    </div>
                    <p className="text-xs mt-0.5" style={{ color: T.muted }}>
                      {h.detail}{h.result ? ` · ${h.result}` : ""}
                    </p>
                    {h.notes && <p className="text-xs mt-1.5 italic" style={{ color: T.chalkDim }}>"{h.notes}"</p>}
                    {!h.notes && <p className="text-[11px] mt-1.5" style={{ color: accent }}>+ add notes</p>}
                  </div>
                </button>
              ))}
            </div>
          )
        )}

        {tab === "prs" && (
          Object.keys(prs).length === 0 ? (
            <p className="text-sm text-center mt-10" style={{ color: T.muted }}>No PRs recorded yet. Log a lift with a weight, or finish a benchmark WOD for time.</p>
          ) : (
            <div className="space-y-2">
              {Object.entries(prs).map(([name, pr]) => (
                <div key={name} className="flex justify-between items-center p-3 rounded-xl" style={cardStyle()}>
                  <div>
                    <p style={{ color: T.chalk, fontWeight: 600 }}>{name}</p>
                    <p className="text-[11px]" style={{ color: T.muted }}>{new Date(pr.date).toLocaleDateString()}</p>
                  </div>
                  <p style={{ ...numeric, color: accent, fontSize: 20, fontWeight: 600 }}>
                    {pr.unit === "time" ? fmt(pr.value) : displayWeight(pr.value, settings.units)}
                  </p>
                </div>
              ))}
            </div>
          )
        )}

        {tab === "data" && (
          <div className="space-y-3">
            <p className="text-sm" style={{ color: T.muted }}>
              Your log lives on this device only. Export it somewhere safe — clearing your browser data would otherwise wipe it.
            </p>
            <button aria-label="Download" onClick={exportCsv} className="w-full flex items-center gap-3 p-4 rounded-xl" style={cardStyle()}>
              <Download size={18} color={accent} />
              <div className="flex-1 text-left">
                <p style={{ color: T.chalk, fontWeight: 600 }}>Workout history (CSV)</p>
                <p className="text-xs" style={{ color: T.muted }}>{history.length} sessions · opens in any spreadsheet</p>
              </div>
            </button>
            <button aria-label="Download" onClick={exportMetrics} className="w-full flex items-center gap-3 p-4 rounded-xl" style={cardStyle()}>
              <Download size={18} color={accent} />
              <div className="flex-1 text-left">
                <p style={{ color: T.chalk, fontWeight: 600 }}>Body metrics (CSV)</p>
                <p className="text-xs" style={{ color: T.muted }}>{metrics.length} check-ins</p>
              </div>
            </button>
            <button aria-label="Download" onClick={exportBackup} className="w-full flex items-center gap-3 p-4 rounded-xl" style={cardStyle()}>
              <Download size={18} color={T.sage} />
              <div className="flex-1 text-left">
                <p style={{ color: T.chalk, fontWeight: 600 }}>Full backup (JSON)</p>
                <p className="text-xs" style={{ color: T.muted }}>Everything, restorable below</p>
              </div>
            </button>

            <label className="w-full flex items-center gap-3 p-4 rounded-xl cursor-pointer" style={{ background: T.surface }}>
              <Upload size={18} color={T.muted} />
              <div className="flex-1 text-left">
                <p style={{ color: T.chalk, fontWeight: 600 }}>Restore from backup</p>
                <p className="text-xs" style={{ color: T.muted }}>Replaces everything currently on this device</p>
              </div>
              <input type="file" accept="application/json,.json" className="hidden"
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (!file) return;
                  const reader = new FileReader();
                  reader.onload = () => {
                    try {
                      const data = JSON.parse(reader.result);
                      if (data.app !== "FitForge") throw new Error("not a FitForge backup");
                      if (confirm(`Restore ${data.history?.length || 0} sessions? This replaces what's on this device.`)) {
                        store.replaceAll(data);
                      }
                    } catch (err) {
                      alert("That file didn't look like a FitForge backup.");
                    }
                  };
                  reader.readAsText(file);
                  e.target.value = "";
                }} />
            </label>
          </div>
        )}
      </div>
      {editing && <NotesEditor session={editing} store={store} onClose={() => setEditing(null)} />}
    </div>
  );
}

/* Daily check-in: bodyweight, sleep, soreness. These are the inputs a coach
   actually asks about, and they feed straight into its recovery calls. */
function MetricsModal({ open, onClose, store }) {
  const { settings, logMetrics, metrics } = store;
  const today = metrics.find((m) => m.day === new Date().toISOString().slice(0, 10));
  const [weight, setWeight] = useState("");
  const [sleep, setSleep] = useState("");
  const [soreness, setSoreness] = useState(0);
  const [note, setNote] = useState("");

  useEffect(() => {
    if (!open) return;
    setWeight(today?.weightKg ? String(Math.round((settings.units === "lb" ? today.weightKg * KG_TO_LB : today.weightKg) * 10) / 10) : "");
    setSleep(today?.sleep ? String(today.sleep) : "");
    setSoreness(today?.soreness || 0);
    setNote(today?.note || "");
  }, [open, today, settings.units]);

  if (!open) return null;

  const save = () => {
    const w = parseFloat(weight);
    logMetrics({
      weightKg: !isNaN(w) ? (settings.units === "lb" ? w / KG_TO_LB : w) : null,
      sleep: parseFloat(sleep) || null,
      soreness: soreness || null,
      note: note.trim(),
    });
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-end" style={{ background: "rgba(22,24,29,0.42)", backdropFilter: "blur(2px)" }}>
      <div className="w-full rounded-t-2xl p-5 max-h-[92vh] overflow-y-auto" style={{ background: T.surface }}>
        <div className="flex items-center justify-between mb-1">
          <h2 style={{ ...display, color: T.chalk, fontSize: 20 }}>Daily check-in</h2>
          <button aria-label="Close" onClick={onClose}><X color={T.muted} /></button>
        </div>
        <p className="text-xs mb-4" style={{ color: T.muted }}>All optional. Your coach uses these when judging recovery.</p>

        <label className="text-xs" style={{ color: T.muted }}>Bodyweight ({settings.units})</label>
        <input value={weight} onChange={(e) => setWeight(e.target.value)} type="number" step="0.1"
          className="w-full p-3 rounded-xl mb-3 mt-1" style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }} />

        <label className="text-xs" style={{ color: T.muted }}>Sleep last night (hours)</label>
        <input value={sleep} onChange={(e) => setSleep(e.target.value)} type="number" step="0.5"
          className="w-full p-3 rounded-xl mb-3 mt-1" style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }} />

        <label className="text-xs" style={{ color: T.muted }}>Soreness</label>
        <div className="flex gap-1 mb-1 mt-1">
          {["None", "Slight", "Moderate", "High", "Severe"].map((l, i) => (
            <button key={l} onClick={() => setSoreness(i + 1)} className="flex-1 py-2 rounded-lg text-[11px]"
              style={{ background: soreness === i + 1 ? T.ember : T.surface2, color: soreness === i + 1 ? T.ink : T.muted, fontWeight: soreness === i + 1 ? 700 : 500 }}>
              {l}
            </button>
          ))}
        </div>
        <div className="mb-3" />

        <label className="text-xs" style={{ color: T.muted }}>Anything else</label>
        <textarea value={note} onChange={(e) => setNote(e.target.value)} rows={2}
          placeholder="Slept badly, travelling this week, knee a bit off"
          className="w-full p-3 rounded-xl mb-4 mt-1 text-sm" style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }} />

        <button onClick={save} className="w-full py-3 rounded-xl" style={{ ...emberButton(), fontSize: 16 }}>
          {today ? "Update today" : "Save check-in"}
        </button>
      </div>
    </div>
  );
}

/* ============================== WEEKLY PLAN ============================== */
const PLAN_TEMPLATES = {
  balanced: { name: "Balanced", days: ["WOD", "Lift — Upper", "Yoga", "Cardio", "Lift — Lower", "WOD", "Rest"] },
  strength: { name: "Strength focus", days: ["Lift — Lower", "Lift — Upper", "Yoga", "Lift — Lower", "Lift — Upper", "Cardio", "Rest"] },
  endurance: { name: "Endurance focus", days: ["Cardio", "WOD", "Cardio", "Yoga", "Cardio — long", "Lift — Full body", "Rest"] },
  conditioning: { name: "Conditioning", days: ["WOD", "Cardio", "WOD", "Yoga", "WOD", "Cardio", "Rest"] },
};
const DAY_NAMES = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

/* Monday-indexed date helpers so "this week" means the same thing everywhere. */
function mondayOf(d = new Date()) {
  const x = new Date(d);
  x.setHours(0, 0, 0, 0);
  x.setDate(x.getDate() - ((x.getDay() + 6) % 7));
  return x;
}
function dayKey(d) {
  return d.toISOString().slice(0, 10);
}

function PlanScreen({ store }) {
  const { plan, setPlan, history } = store;
  const accent = T.amber;
  const todayIdx = (new Date().getDay() + 6) % 7;

  // A day counts as done if anything was logged on it. Real completion beats a
  // manual checkbox nobody remembers to tick.
  const weekStart = mondayOf();
  const loggedDays = useMemo(() => {
    const set = new Set();
    history.forEach((h) => {
      const d = new Date(h.date);
      if (d >= weekStart) set.add((d.getDay() + 6) % 7);
    });
    return set;
  }, [history, weekStart]);

  if (!plan) {
    // The goal chosen during onboarding maps to a template, so the obvious
    // choice is surfaced first instead of being collected and forgotten.
    const GOAL_TO_TEMPLATE = { strength: "strength", endurance: "endurance", conditioning: "conditioning", general: "balanced" };
    const suggested = GOAL_TO_TEMPLATE[store.settings?.goal] || null;
    return (
      <div className="pb-28">
        <TopBar title="Weekly Plan" accent={accent} />
        <div className="px-4">
          <p className="text-sm mb-4" style={{ color: T.muted }}>Pick a template to lay out your week. You can switch or clear it any time.</p>
          <div className="space-y-2">
            {Object.entries(PLAN_TEMPLATES).map(([id, t]) => (
              <button key={id} onClick={() => setPlan({ template: id, days: t.days })} className="w-full text-left p-4 rounded-xl"
                style={cardStyle(id === suggested ? accent : null)}>
                <div className="flex items-center gap-2">
                  <p style={{ ...display, color: T.chalk, fontSize: 16 }}>{t.name}</p>
                  {id === suggested && <span className="text-[10px] px-2 py-0.5 rounded-full" style={{ background: accent, color: T.ink, fontWeight: 700 }}>MATCHES YOUR GOAL</span>}
                </div>
                <p className="text-xs mt-1" style={{ color: T.muted }}>{t.days.filter((d) => d !== "Rest").length} training days · {t.days.filter((d) => d === "Rest").length} rest</p>
              </button>
            ))}
          </div>
        </div>
      </div>
    );
  }

  const trainingDays = plan.days.filter((d) => d !== "Rest").length;
  const doneTraining = plan.days.filter((d, i) => d !== "Rest" && loggedDays.has(i)).length;
  const pct = trainingDays ? Math.round((doneTraining / trainingDays) * 100) : 0;
  const missed = plan.days.filter((d, i) => d !== "Rest" && i < todayIdx && !loggedDays.has(i));

  return (
    <div className="pb-28">
      <TopBar title="Weekly Plan" accent={accent}
        right={<button onClick={() => setPlan(null)} className="text-xs px-3 py-1.5 rounded-full" style={{ background: T.surface2, color: T.muted }}>Change</button>} />
      <div className="px-4">
        <div className="rounded-2xl p-4 mb-4" style={cardStyle()}>
          <div className="flex justify-between items-baseline mb-2">
            <span style={{ ...display, color: T.chalk, fontSize: 15 }}>This week</span>
            <span style={{ ...numeric, color: accent, fontSize: 22, fontWeight: 600 }}>{doneTraining}/{trainingDays}</span>
          </div>
          <div className="h-2 rounded-full overflow-hidden" style={{ background: T.surface2 }}>
            <div style={{ width: `${pct}%`, height: "100%", background: accent, transition: "width .3s" }} />
          </div>
          {missed.length > 0 && (
            <p className="text-xs mt-2" style={{ color: T.muted }}>
              {missed.length} session{missed.length > 1 ? "s" : ""} missed so far — not a problem unless it becomes a pattern.
            </p>
          )}
        </div>

        <div className="space-y-2">
          {plan.days.map((d, i) => {
            const done = loggedDays.has(i);
            const isToday = i === todayIdx;
            const isRest = d === "Rest";
            const past = i < todayIdx;
            return (
              <div key={i} className="flex items-center gap-3 p-3 rounded-xl"
                style={{
                  background: isToday ? T.amberDim : T.surface,
                  border: isToday ? `1px solid ${accent}55` : "none",
                  opacity: past && !done && !isRest ? 0.55 : 1,
                }}>
                <span style={{ ...display, color: isToday ? accent : T.muted, fontSize: 14, width: 38 }}>{DAY_NAMES[i]}</span>
                <span className="flex-1" style={{ color: isRest ? T.muted : T.chalk, fontWeight: 600, textDecoration: done && !isRest ? "line-through" : "none" }}>{d}</span>
                {done && !isRest && <Check size={16} color={T.sage} />}
                {isToday && !done && <span className="text-[10px] px-2 py-0.5 rounded-full" style={{ background: accent, color: T.ink, fontWeight: 700 }}>TODAY</span>}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

/* ============================== FIRST RUN ==============================
   Not the three-step form this replaced. One question, three answers, and you
   land in a workout — because the fastest way to understand a training app is
   to be doing something in it, not reading about it.

   Each answer sets a goal, a plan template and an equipment baseline, all of
   which stay editable in Settings afterwards.
==================================================================== */
const START_INTENTS = [
  {
    id: "move", label: "Just get moving", sub: "Short sessions, nothing technical",
    goal: "general", plan: "balanced", level: "beginner",
    equipment: { bodyweight: true }, icon: HeartPulse, color: "cyan",
  },
  {
    id: "strong", label: "Get stronger", sub: "Lifting, with weights if you have them",
    goal: "strength", plan: "strength", level: "intermediate",
    equipment: { bodyweight: true, dumbbells: true, kettlebell: true }, icon: Dumbbell, color: "ember",
  },
  {
    id: "fit", label: "Get fitter", sub: "Conditioning and cardio",
    goal: "conditioning", plan: "conditioning", level: "intermediate",
    equipment: { bodyweight: true, space_to_run: true, jump_rope: true }, icon: Flame, color: "amber",
  },
  {
    id: "calm", label: "Wind down", sub: "Yoga, mobility and breathing",
    goal: "general", plan: "balanced", level: "beginner",
    equipment: { bodyweight: true }, icon: Wind, color: "sage",
  },
];

function FirstRun({ store, onDone }) {
  const { setSettings, setEquipment, setPlan, settings } = store;

  const choose = (intent) => {
    setSettings({ ...settings, goal: intent.goal, level: intent.level, seenIntro: true });
    setEquipment(intent.equipment);
    setPlan({ template: intent.plan, days: PLAN_TEMPLATES[intent.plan].days });
    onDone(intent);
  };

  const skip = () => { setSettings({ ...settings, seenIntro: true }); onDone(null); };

  return (
    <div style={{ background: T.ink, minHeight: "100vh" }} className="flex flex-col justify-center px-6 py-12">
      <p style={{ ...display, color: T.ember, fontSize: 12, letterSpacing: "0.18em" }}>FITFORGE</p>
      <h1 style={{ color: T.chalk, fontSize: 32, fontWeight: 800, lineHeight: 1.12 }} className="mt-1 mb-1.5">
        What do you want<br />to do today?
      </h1>
      <p className="text-sm mb-6" style={{ color: T.muted }}>
        Pick one and I'll set up your week and suggest things to try.
        Nothing starts until you say so, and you can change all of it later.
      </p>

      <div className="space-y-2.5">
        {START_INTENTS.map((i) => {
          const Icon = i.icon;
          return (
            <button key={i.id} onClick={() => choose(i)} className="w-full flex items-center gap-3.5 p-4 rounded-[20px] text-left"
              style={cardStyle()}>
              <span className="w-11 h-11 rounded-[13px] flex items-center justify-center shrink-0" style={{ background: T[i.color + "Dim"] }}>
                <Icon size={20} color={T[i.color]} />
              </span>
              <span className="flex-1">
                <span className="block" style={{ color: T.chalk, fontWeight: 700, fontSize: 15.5 }}>{i.label}</span>
                <span className="block text-[12px] mt-0.5" style={{ color: T.muted }}>{i.sub}</span>
              </span>
              <ChevronRight size={17} color={T.muted} />
            </button>
          );
        })}
      </div>

      <button onClick={skip} className="mt-6 text-[13px] mx-auto" style={{ color: T.muted }}>
        I'll look around myself
      </button>
    </div>
  );
}

/* ============================== SETTINGS ============================== */
function SettingsModal({ open, onClose, store }) {
  if (!open) return null;
  const { settings, setSettings, setHistory } = store;
  return (
    <div className="fixed inset-0 z-50 flex items-end" style={{ background: "rgba(22,24,29,0.42)", backdropFilter: "blur(2px)" }}>
      <div className="w-full rounded-t-2xl p-5 max-h-[85vh] overflow-y-auto" style={{ background: T.surface }}>
        <div className="flex items-center justify-between mb-5">
          <h2 style={{ ...display, color: T.chalk, fontSize: 20 }}>Settings</h2>
          <button aria-label="Close" onClick={onClose}><X color={T.muted} /></button>
        </div>

        <p style={{ ...display, color: T.muted, fontSize: 12 }} className="mb-2">Training goal</p>
        <div className="grid grid-cols-2 gap-2 mb-5">
          {[["general", "General fitness"], ["strength", "Get stronger"], ["endurance", "Build endurance"], ["conditioning", "Conditioning"]].map(([id, label]) => (
            <button key={id} onClick={() => setSettings({ ...settings, goal: id })} className="py-2.5 rounded-xl text-xs"
              style={{ background: settings.goal === id ? T.ember : T.surface2, color: settings.goal === id ? T.onAccent : T.chalkDim, fontWeight: settings.goal === id ? 700 : 500 }}>
              {label}
            </button>
          ))}
        </div>

        <p style={{ ...display, color: T.muted, fontSize: 12 }} className="mb-2">Experience</p>
        <div className="flex gap-2 mb-5">
          {[["beginner", "New"], ["intermediate", "Comfortable"], ["advanced", "Experienced"]].map(([id, label]) => (
            <button key={id} onClick={() => setSettings({ ...settings, level: id })} className="flex-1 py-2.5 rounded-xl text-xs"
              style={{ background: settings.level === id ? T.ember : T.surface2, color: settings.level === id ? T.onAccent : T.chalkDim, fontWeight: settings.level === id ? 700 : 500 }}>
              {label}
            </button>
          ))}
        </div>

        <p style={{ ...display, color: T.muted, fontSize: 12 }} className="mb-2">Units</p>
        <div className="flex gap-2 mb-5">
          {["kg", "lb"].map((u) => (
            <button key={u} onClick={() => setSettings({ ...settings, units: u })} className="flex-1 py-2.5 rounded-xl"
              style={{ background: settings.units === u ? T.ember : T.surface2, color: settings.units === u ? T.onAccent : T.chalk, fontWeight: 700 }}>
              {u.toUpperCase()}
            </button>
          ))}
        </div>

        <p style={{ ...display, color: T.muted, fontSize: 12 }} className="mb-2">Sound</p>

        <button onClick={() => { const on = !settings.sound; setSettings({ ...settings, sound: on }); if (on) Sound.go(); }}
          className="w-full flex items-center justify-between py-3 border-b" style={{ borderColor: T.line }}>
          <span className="text-left">
            <span className="block" style={{ color: T.chalk }}>Sound cues</span>
            <span className="block text-[11px]" style={{ color: T.muted }}>Countdowns, round changes, breath pacing</span>
          </span>
          <span className="w-11 h-6 rounded-full relative shrink-0" style={{ background: settings.sound ? T.ember : T.line }}>
            <span className="absolute top-0.5 w-5 h-5 rounded-full" style={{ left: settings.sound ? 22 : 2, background: T.surface }} />
          </span>
        </button>

        <button onClick={() => { const on = !settings.voice; setSettings({ ...settings, voice: on }); if (on) Sound.say("Breathe in", true); }}
          className="w-full flex items-center justify-between py-3 border-b" style={{ borderColor: T.line }}>
          <span className="text-left">
            <span className="block" style={{ color: T.chalk }}>Spoken cues</span>
            <span className="block text-[11px]" style={{ color: T.muted }}>Says each breath phase out loud</span>
          </span>
          <span className="w-11 h-6 rounded-full relative shrink-0" style={{ background: settings.voice ? T.ember : T.line }}>
            <span className="absolute top-0.5 w-5 h-5 rounded-full" style={{ left: settings.voice ? 22 : 2, background: T.surface }} />
          </span>
        </button>

        <div className="py-3 border-b mb-4" style={{ borderColor: T.line }}>
          <div className="flex items-center justify-between mb-2">
            <span style={{ color: T.chalk }}>Volume</span>
            <span style={{ ...numeric, color: T.muted, fontSize: 12 }}>{Math.round((settings.volume ?? 0.6) * 100)}%</span>
          </div>
          <input
            type="range" min="0" max="1" step="0.05"
            value={settings.volume ?? 0.6}
            onChange={(e) => setSettings({ ...settings, volume: Number(e.target.value) })}
            onMouseUp={() => Sound.tick()}
            onTouchEnd={() => Sound.tick()}
            className="w-full"
            style={{ accentColor: T.ember }}
          />
        </div>

        <button onClick={() => { if (confirm("Clear all logged sessions? This can't be undone.")) setHistory([]); }}
          className="w-full py-3 rounded-xl mb-3" style={{ background: T.surface2, color: T.ember }}>
          Clear workout history
        </button>
        <button onClick={onClose} className="w-full py-3 rounded-xl" style={{ ...emberButton(), fontSize: 16 }}>Done</button>
      </div>
    </div>
  );
}

/* ============================== REST TIMER ============================== */
function RestTimer({ soundOn, accent = T.amber }) {
  const timer = useTimer(soundOn);
  const presets = [60, 90, 120, 180];
  return (
    <div className="rounded-2xl p-5 text-center" style={cardStyle()}>
      <p style={{ ...display, color: T.muted, fontSize: 13 }}>Rest Timer</p>
      <p style={{ ...numeric, color: T.chalk, fontSize: 54, fontWeight: 600 }} className="my-2">{fmt(timer.seconds)}</p>
      <div className="flex justify-center gap-2 mb-4">
        {presets.map((p) => (
          <button key={p} onClick={() => timer.start("countdown", { total: p })} className="text-xs px-3 py-1.5 rounded-full" style={{ background: T.surface2, color: T.chalk }}>
            {p < 120 ? `${p}s` : `${p / 60}m`}
          </button>
        ))}
      </div>
      {timer.mode && (
        <div className="flex justify-center gap-3">
          {timer.running
            ? <button aria-label="Pause" onClick={timer.pause} className="p-3 rounded-full" style={{ background: T.surface2 }}><Pause size={18} color={T.chalk} /></button>
            : <button aria-label="Play" onClick={timer.resume} className="p-3 rounded-full" style={{ background: accent }}><Play size={18} color={T.onAccent} /></button>}
          <button aria-label="Reset" onClick={timer.reset} className="p-3 rounded-full" style={{ background: T.surface2 }}><RotateCcw size={18} color={T.chalk} /></button>
        </div>
      )}
    </div>
  );
}

/* ============================== LOG SESSION MODAL ============================== */
function LogModal({ open, onClose, store, preset }) {
  const [name, setName] = useState(preset?.name || "");
  const [result, setResult] = useState("");
  const [load, setLoad] = useState("");
  const [rpe, setRpe] = useState(0);
  const [notes, setNotes] = useState("");
  const [debrief, setDebrief] = useState(null);
  const [asking, setAsking] = useState(false);
  useEffect(() => {
    if (open) { setName(preset?.name || ""); setResult(""); setLoad(""); setRpe(0); setNotes(""); setDebrief(null); setAsking(false); }
  }, [open, preset]);
  if (!open) return null;
  const { logSession, recordPr, settings } = store;

  // Ask the coach to react to what was just written, before the modal closes.
  const askDebrief = async () => {
    setAsking(true);
    try {
      const ctx = buildTrainingContext(store);
      const summary = `${name || "Workout"}${result ? " — " + result : ""}${rpe ? ` (RPE ${rpe})` : ""}. ${notes}`;
      setDebrief(await askCoach("debrief", ctx, summary));
    } catch (e) {
      setDebrief({ response: "Couldn't reach the coach right now — your session still saved fine.", adjustment: "" });
    }
    setAsking(false);
  };

  const save = () => {
    const loadNum = parseFloat(load);
    const loadKg = !isNaN(loadNum) ? (settings.units === "lb" ? loadNum / KG_TO_LB : loadNum) : null;
    logSession({
      type: preset?.type || "wod",
      name: name || "Workout",
      detail: preset?.detail || "",
      result,
      loadKg,
      rpe: rpe || null,
      notes: notes.trim(),
      pattern: preset?.pattern,
    });
    if (loadKg) recordPr(name || "Workout", loadKg, "load");
    const secs = parseResultTime(result);
    if (secs) recordPr(name || "Workout", secs, "time");
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-end" style={{ background: "rgba(22,24,29,0.42)", backdropFilter: "blur(2px)" }}>
      <div className="w-full rounded-t-2xl p-5 max-h-[92vh] overflow-y-auto" style={{ background: T.surface }}>
        <div className="flex items-center justify-between mb-4">
          <h2 style={{ ...display, color: T.chalk, fontSize: 20 }}>Log Session</h2>
          <button aria-label="Close" onClick={onClose}><X color={T.muted} /></button>
        </div>

        <label className="text-xs" style={{ color: T.muted }}>Name</label>
        <input value={name} onChange={(e) => setName(e.target.value)} className="w-full p-3 rounded-xl mb-3 mt-1" style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }} />

        <label className="text-xs" style={{ color: T.muted }}>Result (time, rounds, or reps)</label>
        <input value={result} onChange={(e) => setResult(e.target.value)} placeholder="e.g. 8:42 or 6 rounds" className="w-full p-3 rounded-xl mb-3 mt-1" style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }} />

        <label className="text-xs" style={{ color: T.muted }}>Top weight ({settings.units}) — optional</label>
        <input value={load} onChange={(e) => setLoad(e.target.value)} type="number" className="w-full p-3 rounded-xl mb-3 mt-1" style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }} />

        <div className="flex items-center justify-between">
          <label className="text-xs" style={{ color: T.muted }}>How hard did that feel?</label>
          <LoadRing rpe={rpe} size={38} />
        </div>
        <div className="flex gap-1 mb-1 mt-1">
          {[...Array(10)].map((_, i) => {
            const c = plateForIntensity(i + 1);
            const on = rpe === i + 1;
            return (
              <button key={i} onClick={() => setRpe(i + 1)} className="flex-1 py-2 rounded-lg"
                style={{ ...numeric, background: on ? c : T.surface2, color: on ? T.onAccent : T.muted, fontWeight: on ? 700 : 500, fontSize: 12, border: `1px solid ${on ? c : "transparent"}` }}>
                {i + 1}
              </button>
            );
          })}
        </div>
        <p className="text-[10px] mb-3" style={{ color: T.muted }}>1 = easy · 10 = everything you had</p>

        <label className="text-xs" style={{ color: T.muted }}>Notes — how it went, what hurt, what to change</label>
        <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={4}
          placeholder="Left shoulder tight on overhead work. Grip failed before legs did."
          className="w-full p-3 rounded-xl mb-4 mt-1 text-sm" style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }} />

        {notes.trim() && !debrief && (
          <button onClick={askDebrief} disabled={asking}
            className="w-full py-2.5 rounded-xl mb-3 flex items-center justify-center gap-2 text-sm"
            style={{ background: "#241B2E", color: "#B78BE0", border: "1px solid #B78BE044", opacity: asking ? 0.6 : 1 }}>
            <Sparkles size={15} /> {asking ? "Coach is reading…" : "Get coach feedback on this"}
          </button>
        )}

        {debrief && (
          <div className="rounded-xl p-3 mb-3" style={{ background: "#241B2E", border: "1px solid #B78BE044" }}>
            <p className="text-sm" style={{ color: T.chalk }}>{debrief.response}</p>
            {debrief.adjustment && (
              <p className="text-xs mt-2" style={{ color: "#B78BE0" }}>Next time: {debrief.adjustment}</p>
            )}
          </div>
        )}

        <button onClick={save} className="w-full py-3 rounded-xl" style={{ ...emberButton(), fontSize: 16 }}>Save</button>
      </div>
    </div>
  );
}

/* Edit a past session's notes — the coach reads these, so backfilling matters. */
function NotesEditor({ session, store, onClose }) {
  const [notes, setNotes] = useState(session.notes || "");
  const [rpe, setRpe] = useState(session.rpe || 0);
  return (
    <div className="fixed inset-0 z-50 flex items-end" style={{ background: "rgba(22,24,29,0.42)", backdropFilter: "blur(2px)" }}>
      <div className="w-full rounded-t-2xl p-5 max-h-[92vh] overflow-y-auto" style={{ background: T.surface }}>
        <div className="flex items-center justify-between mb-1">
          <h2 style={{ ...display, color: T.chalk, fontSize: 20 }}>{session.name}</h2>
          <button aria-label="Close" onClick={onClose}><X color={T.muted} /></button>
        </div>
        <p className="text-xs mb-4" style={{ color: T.muted }}>{new Date(session.date).toLocaleString()}</p>

        <label className="text-xs" style={{ color: T.muted }}>How hard did that feel?</label>
        <div className="flex gap-1 mb-4 mt-1">
          {[...Array(10)].map((_, i) => {
            const c = plateForIntensity(i + 1);
            const on = rpe === i + 1;
            return (
              <button key={i} onClick={() => setRpe(i + 1)} className="flex-1 py-2 rounded-lg"
                style={{ ...numeric, background: on ? c : T.surface2, color: on ? T.onAccent : T.muted, fontWeight: on ? 700 : 500, fontSize: 12 }}>
                {i + 1}
              </button>
            );
          })}
        </div>

        <label className="text-xs" style={{ color: T.muted }}>Notes</label>
        <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={6}
          className="w-full p-3 rounded-xl mb-4 mt-1 text-sm" style={{ background: T.surface2, color: T.chalk, border: `1px solid ${T.line}` }} />

        <button onClick={() => { store.updateSession(session.id, { notes: notes.trim(), rpe: rpe || null }); onClose(); }}
          className="w-full py-3 rounded-xl" style={{ ...emberButton(), fontSize: 16 }}>Save changes</button>
      </div>
    </div>
  );
}

/* ============================== RADIAL NAV HUB ==============================
   Thirteen destinations won't fit in a tab bar, and a hamburger list buries
   them. This fans them out on two arcs around the thumb instead: everything
   is one tap from anywhere, reachable one-handed, and the arc positions stay
   fixed so they become muscle memory.

   Angles sweep the upper half-circle (180deg to 360deg) since the hub sits at
   the bottom of the screen. Petals stagger outward so the arcs read as two
   distinct rings rather than one cloud.
==================================================================== */
/* ============================== BUILD IT (TALK) ==============================
   Say or type what you want and get a workout built around it. The parsing
   and building live in talk.js and run entirely on the device, so this works
   offline and in the installed app. Follow-ups refine the last request:
   "harder", "make it 30 minutes", "swap the burpees".
==================================================================== */
const TALK_LIB = { EX, EQUIPMENT, TECHNICAL, DIFFICULTY, BREATH_PATTERNS, YOGA_AREAS, scaleReps, scaleDistance, jitter };
export { TALK_LIB };

const TALK_EXAMPLES = [
  "20 minutes, legs and core, no jumping",
  "Quick upper body with dumbbells",
  "Hard 30 min full body, include pull-ups",
  "Gentle 15 minutes — my lower back is sore",
  "Strength day: squats and bench",
  "Something to help me sleep",
];

/* Speech recognition is built into Chrome, Edge and Safari (as
   webkitSpeechRecognition). Firefox has none, and installed iOS home-screen
   apps don't always expose it — the keyboard's own dictation key still works
   there, so the text box is the fallback rather than an error. */
function useSpeechInput({ onFinal, onInterim }) {
  const recRef = useRef(null);
  const [listening, setListening] = useState(false);
  const [error, setError] = useState(null);
  const SR = typeof window !== "undefined" ? (window.SpeechRecognition || window.webkitSpeechRecognition) : null;
  const finalRef = useRef("");

  /* Inside a claude.ai artifact the browser's recognizer has no microphone,
     but the viewer can grant the platform's dictation instead. It's resolved
     ahead of time because start() must run straight from the tap. */
  const dictRef = useRef(null);
  const handlers = useRef({ onFinal, onInterim });
  handlers.current = { onFinal, onInterim };
  const [hasDictation, setHasDictation] = useState(false);
  useEffect(() => {
    let off = [];
    claudeCapability("dictation").then((d) => {
      if (!d) return;
      dictRef.current = d;
      setHasDictation(true);
      off = [
        d.onPartial(({ text }) => handlers.current.onInterim?.((finalRef.current + " " + text).trim())),
        d.onFinal(({ text }) => { finalRef.current = (finalRef.current + " " + text).trim(); handlers.current.onInterim?.(finalRef.current); }),
        d.onState(({ state, reason }) => {
          if (state === "listening" || state === "starting") { setListening(true); return; }
          if (state !== "idle") return;
          setListening(false);
          if (reason === "not_granted") setError("Microphone access was declined. Type instead.");
          else if (reason === "unavailable" || reason === "upstream_error") setError("Voice input isn't available right now. Type instead.");
          const text = finalRef.current.trim();
          finalRef.current = "";
          if (text) handlers.current.onFinal?.(text);
        }),
      ];
    });
    return () => off.forEach((f) => { try { f(); } catch (e) { /* ignore */ } });
  }, []);
  const supported = hasDictation || !!SR;

  const stop = () => {
    if (dictRef.current) { dictRef.current.stop().catch(() => {}); return; }
    try { recRef.current?.stop(); } catch (e) { /* already stopped */ }
  };

  const start = () => {
    if (dictRef.current) {
      setError(null);
      Sound.stopSpeech();
      finalRef.current = "";
      dictRef.current.start().catch((e) => {
        setListening(false);
        if (e?.code === "not_granted") setError("Microphone access was declined. Type instead.");
        else if (e?.code !== "dismissed") setError("Voice input couldn't start. Type it instead.");
      });
      return;
    }
    if (!SR) return;
    setError(null);
    Sound.stopSpeech();
    try {
      const rec = new SR();
      rec.lang = navigator.language || "en-US";
      rec.interimResults = true;
      rec.continuous = false;
      rec.maxAlternatives = 1;
      finalRef.current = "";
      rec.onresult = (e) => {
        let interim = "";
        for (let i = e.resultIndex; i < e.results.length; i++) {
          const r = e.results[i];
          if (r.isFinal) finalRef.current += r[0].transcript;
          else interim += r[0].transcript;
        }
        onInterim?.((finalRef.current + " " + interim).trim());
      };
      rec.onerror = (e) => {
        const msg = {
          "not-allowed": "Microphone access is blocked. Allow it in your browser settings, or type instead.",
          "service-not-allowed": "Voice input isn't available here. Your keyboard's dictation key works too.",
          "no-speech": "I didn't hear anything — tap the mic and try again.",
          "audio-capture": "No microphone found.",
          network: "Voice input needs a connection on this browser. Typing works offline.",
        }[e.error];
        if (e.error !== "aborted") setError(msg || "Voice input stopped. Try again, or type it.");
      };
      rec.onend = () => {
        setListening(false);
        recRef.current = null;
        const text = finalRef.current.trim();
        if (text) onFinal?.(text);
      };
      recRef.current = rec;
      rec.start();
      setListening(true);
    } catch (e) {
      setListening(false);
      setError("Voice input couldn't start. Type it instead.");
    }
  };

  useEffect(() => () => { try { recRef.current?.abort(); } catch (e) { /* ignore */ } }, []);
  return { supported, listening, error, start, stop, clearError: () => setError(null) };
}

/* Reads a reply aloud: the platform's voice inside a claude.ai artifact (where
   it's allowed), otherwise the browser's built-in speech. */
function speakReply(text) {
  claudeCapability("tts").then((t) => {
    if (t) t.speak(text).catch(() => Sound.say(text, true));
    else Sound.say(text, true);
  });
}

function talkSignature(w) {
  return w ? [w.format, w.meta, ...(w.movements || []).map((m) => m.name + m.reps)].join("|") : "";
}

function HeardChips({ chips }) {
  const tone = {
    exclude: [T.emberDim, T.emberInk], avoid: [T.amberDim, T.amberInk], include: [T.sageDim, T.sageInk],
    focus: [T.cyanDim, T.cyanInk],
  };
  return (
    <div className="flex flex-wrap gap-1.5">
      {chips.map((c, i) => {
        const [bg, fg] = tone[c.kind] || [T.surface2, T.chalkDim];
        const prefix = c.kind === "exclude" ? "No " : c.kind === "include" ? "+ " : "";
        return (
          <span key={i} className="px-2.5 py-1 rounded-full text-[11.5px]"
            title={c.assumed ? "You didn't say — I filled this in" : "From what you said"}
            style={{
              background: c.assumed ? "transparent" : bg, color: c.assumed ? T.muted : fg, fontWeight: 600,
              border: c.assumed ? `1px dashed ${T.line}` : `1px solid ${bg}`,
            }}>
            {prefix}{c.text}{c.assumed ? " ·  default" : ""}
          </span>
        );
      })}
    </div>
  );
}

function TalkWorkoutCard({ result, onStart, onRefine, onOpenYoga, onOpenBreath, latest }) {
  const { wod, heard, notes, recovery } = result;
  const accent = wod.level ? DIFFICULTY[wod.level].color : T.ember;
  return (
    <div className="rounded-[20px] p-4" style={cardStyle()}>
      <p style={{ ...display, color: T.muted, fontSize: 10, letterSpacing: "0.16em" }} className="mb-1.5">WHAT I HEARD</p>
      <HeardChips chips={heard} />

      <div className="mt-4 rounded-2xl p-4" style={{ background: T.surface2 }}>
        <div className="flex items-start justify-between gap-2">
          <div>
            <p style={{ color: T.chalk, fontWeight: 800, fontSize: 18, lineHeight: 1.2 }}>{wod.title}</p>
            <p className="text-[12.5px] mt-0.5" style={{ color: T.chalkDim }}>{wod.meta}</p>
          </div>
          <span className="px-2 py-0.5 rounded-full text-[10px] shrink-0" style={{ background: accent, color: T.onAccent, ...display }}>
            {wod.difficulty}
          </span>
        </div>
        <ol className="mt-3 space-y-1.5">
          {wod.movements.map((m, i) => (
            <li key={i} className="flex items-baseline gap-2.5">
              <span style={{ ...numeric, color: accent, fontSize: 14, minWidth: 54, fontWeight: 700 }}>{m.reps}</span>
              <span className="flex-1 text-[14px]" style={{ color: T.chalk }}>
                {m.name}
                {m.load && <span className="block text-[11px]" style={{ color: T.muted }}>{m.load}</span>}
              </span>
            </li>
          ))}
        </ol>
        {wod.est && wod.format !== "AMRAP" && wod.format !== "EMOM" && (
          <p className="text-[11px] mt-2.5 flex items-center gap-1" style={{ color: T.muted }}>
            <Clock size={11} /> Roughly {wod.est} min
          </p>
        )}
      </div>

      {notes.length > 0 && (
        <ul className="mt-3 space-y-1">
          {notes.map((n, i) => (
            <li key={i} className="text-[12px] flex gap-1.5" style={{ color: T.chalkDim, lineHeight: 1.45 }}>
              <span style={{ color: T.amber }}>•</span><span>{n}</span>
            </li>
          ))}
        </ul>
      )}

      {recovery && (recovery.yoga || recovery.breath) && (
        <div className="mt-3 flex flex-col gap-1.5">
          {recovery.yoga && (
            <button onClick={() => onOpenYoga(recovery.yoga)} className="w-full flex items-center gap-2 p-2.5 rounded-xl text-left"
              style={{ background: T.sageDim }}>
              <Wind size={15} color={T.sageInk} />
              <span className="flex-1 text-[12.5px]" style={{ color: T.sageInk, fontWeight: 600 }}>Afterwards: {recovery.yoga} yoga flow</span>
              <ChevronRight size={14} color={T.sageInk} />
            </button>
          )}
          {recovery.breath && (
            <button onClick={() => onOpenBreath(recovery.breath)} className="w-full flex items-center gap-2 p-2.5 rounded-xl text-left"
              style={{ background: T.breathDim }}>
              <Wind size={15} color={T.breathInk} />
              <span className="flex-1 text-[12.5px]" style={{ color: T.breathInk, fontWeight: 600 }}>
                Afterwards: {BREATH_PATTERNS.find((p) => p.id === recovery.breath)?.name || "breathing"}
              </span>
              <ChevronRight size={14} color={T.breathInk} />
            </button>
          )}
        </div>
      )}

      <div className="flex gap-2 mt-4">
        <button onClick={() => onStart(wod)} className="flex-1 py-3 rounded-full flex items-center justify-center gap-2"
          style={{ ...emberButton(), fontSize: 15 }}>
          <Play size={16} /> Start this
        </button>
        {latest && (
          <button aria-label="Shuffle" onClick={() => onRefine("shuffle")} className="px-4 rounded-full flex items-center justify-center"
            style={{ background: T.surface2, color: T.chalkDim }}>
            <Shuffle size={17} />
          </button>
        )}
      </div>
      {latest && (
        <div className="flex gap-1.5 mt-2.5 flex-wrap">
          {["Harder", "Easier", "Shorter", "Longer"].map((r) => (
            <button key={r} onClick={() => onRefine(r.toLowerCase())} className="px-3 py-1.5 rounded-full text-[12px]"
              style={{ background: T.surface, color: T.chalkDim, border: `1px solid ${T.line}`, fontWeight: 600 }}>
              {r}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

function TalkReply({ turn, latest, onStart, onRefine, onOpenYoga, onOpenBreath }) {
  const r = turn.result;
  if (r.kind === "workout") {
    return <TalkWorkoutCard result={r} latest={latest} onStart={onStart} onRefine={onRefine} onOpenYoga={onOpenYoga} onOpenBreath={onOpenBreath} />;
  }
  if (r.kind === "recovery") {
    const bp = r.recovery.breath;
    return (
      <div className="rounded-[20px] p-4" style={cardStyle()}>
        <p className="text-[14px]" style={{ color: T.chalk, lineHeight: 1.5 }}>{r.say}</p>
        <div className="flex flex-col gap-2 mt-3">
          {r.recovery.yoga && (
            <button onClick={() => onOpenYoga(r.recovery.yoga)} className="w-full flex items-center gap-3 p-3 rounded-xl text-left" style={{ background: T.sageDim }}>
              <IconBadge pose="yoga" color={T.sage} size={34} />
              <span className="flex-1 text-sm" style={{ color: T.sageInk, fontWeight: 700 }}>{r.recovery.yoga} flow</span>
              <ChevronRight size={16} color={T.sageInk} />
            </button>
          )}
          {bp && (
            <button onClick={() => onOpenBreath(bp.id)} className="w-full flex items-center gap-3 p-3 rounded-xl text-left" style={{ background: T.breathDim }}>
              <Wind size={20} color={T.breathInk} />
              <span className="flex-1 text-sm" style={{ color: T.breathInk, fontWeight: 700 }}>
                {bp.name} <span style={{ fontWeight: 500 }}>· {bp.tag}</span>
              </span>
              <ChevronRight size={16} color={T.breathInk} />
            </button>
          )}
        </div>
        {latest && (
          <p className="text-[11.5px] mt-3" style={{ color: T.muted }}>Want a workout after all? Just say so — "15 minutes, easy".</p>
        )}
      </div>
    );
  }
  if (r.kind === "coach") {
    return (
      <div className="rounded-[20px] p-4" style={{ ...cardStyle(), border: `1px solid ${T.violet}33` }}>
        <p className="flex items-center gap-1.5 mb-2" style={{ ...display, color: T.violetInk, fontSize: 10, letterSpacing: "0.16em" }}>
          <Sparkles size={12} /> AI COACH
        </p>
        {r.why && <p className="text-[13px] mb-3" style={{ color: T.chalkDim, lineHeight: 1.5 }}>{r.why}</p>}
        <TalkWorkoutCard result={{ wod: r.wod, heard: [], notes: r.note ? [r.note] : [], recovery: null }} latest={false}
          onStart={onStart} onRefine={onRefine} onOpenYoga={onOpenYoga} onOpenBreath={onOpenBreath} />
      </div>
    );
  }
  const warn = r.kind === "stop";
  return (
    <div className="rounded-[20px] p-4" style={{ ...cardStyle(), background: warn ? T.emberDim : T.surface }}>
      <p className="text-[14px]" style={{ color: warn ? T.emberInk : T.chalk, lineHeight: 1.5, fontWeight: warn ? 600 : 400 }}>{r.say}</p>
    </div>
  );
}

function TalkScreen({ store, onLoadWod, onOpenYoga, onOpenBreath, openEquipment }) {
  const { equipment, settings } = store;
  const [turns, setTurns] = useState([]);
  const [draft, setDraft] = useState("");
  const [coachBusy, setCoachBusy] = useState(false);
  const [recent, setRecent] = useState([]);
  const intentRef = useRef(null);
  const lastWodRef = useRef(null);
  const lastKeepRef = useRef(null);
  const endRef = useRef(null);
  const inputRef = useRef(null);

  useEffect(() => { loadKey("ff-talk-recent", []).then((r) => setRecent(Array.isArray(r) ? r : [])); }, []);
  // Bring the newest request to the top so its reply reads from the start;
  // the card's buttons are then a short scroll away, never under the composer.
  const lastUserRef = useRef(null);
  useEffect(() => {
    (lastUserRef.current || endRef.current)?.scrollIntoView({ behavior: "smooth", block: "start" });
  }, [turns.length]);
  useEffect(() => { if (coachBusy) endRef.current?.scrollIntoView({ behavior: "smooth", block: "end" }); }, [coachBusy]);
  // The box grows with what's typed or heard, up to a few lines.
  useEffect(() => {
    const el = inputRef.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(120, el.scrollHeight)}px`;
  }, [draft]);

  const availEquip = useMemo(() => Object.keys(equipment).filter((k) => equipment[k]), [equipment]);

  const respond = (text, via) => {
    const clean = text.trim();
    if (!clean) return;
    const parsed = parseRequest(clean, TALK_LIB);
    const prev = intentRef.current;

    let result;
    let intent;
    if (!parsed.understood && prev) {
      result = { kind: "note", say: "I didn't catch a change there. Try \"harder\", \"20 minutes\", \"no burpees\", \"add pull-ups\" — or describe a whole new workout." };
    } else {
      intent = mergeIntent(prev, parsed);
      // A shuffle should actually look different; anything else should land
      // even if the dice happen to repeat.
      const avoid = parsed.delta.reroll ? talkSignature(lastWodRef.current) : null;
      // "Harder", "shorter", "swap the swings" adjust the workout on screen;
      // a new focus, new kit, a new format or a shuffle builds a fresh one.
      const refining = prev && !parsed.delta.reroll && !parsed.delta.reset && !parsed.focus.length && !parsed.avoidFocus.length
        && !parsed.equipment.length && !parsed.bodyweightOnly && !parsed.fullGym && !parsed.format && !parsed.goal && !parsed.cardio;
      const keep = refining ? lastKeepRef.current : null;
      for (let i = 0; i < 10; i++) {
        result = buildTalkWorkout(intent, TALK_LIB, { availEquip, settings, keep });
        if (result.kind !== "workout" || talkSignature(result.wod) !== avoid) break;
      }
      if (!parsed.understood && !prev && result.kind === "workout") {
        result = { ...result, notes: ["I didn't pick out anything specific, so here's a balanced session. Tell me the time, focus or anything to avoid and I'll reshape it.", ...result.notes] };
      }
      intentRef.current = result.kind === "stop" ? prev : intent;
      if (result.kind === "workout") { lastWodRef.current = result.wod; lastKeepRef.current = result.keep; }
    }

    setTurns((t) => [...t, { role: "user", text: clean, via }, { role: "app", result }]);
    if (via === "voice" && result.say) speakReply(result.say);

    // Remember whole requests (not one-word tweaks) for one-tap reuse.
    if (clean.split(/\s+/).length >= 3 && parsed.understood) {
      const next = [clean, ...recent.filter((r) => r.toLowerCase() !== clean.toLowerCase())].slice(0, 4);
      setRecent(next);
      saveKey("ff-talk-recent", next);
    }
  };

  const speech = useSpeechInput({
    onInterim: (t) => setDraft(t),
    onFinal: (t) => { setDraft(""); respond(t, "voice"); },
  });

  const send = () => { const t = draft; setDraft(""); respond(t, "text"); };
  const startOver = () => { intentRef.current = null; lastWodRef.current = null; lastKeepRef.current = null; setTurns([]); Sound.stopSpeech(); };

  const askAiCoach = async () => {
    const said = intentRef.current?.texts?.join(". ") || "";
    setCoachBusy(true);
    try {
      const out = await askCoach("session", buildTrainingContext(store), said);
      const moves = (out.movements || []).map((m) => {
        const hit = matchExercise(m.name);
        return { name: m.name, reps: m.reps, percent1RM: m.percent1RM || null, pattern: hit?.pattern || "core", prop: hit?.prop, scale: hit?.scale };
      });
      if (!moves.length) throw new Error("no movements");
      store.recordAdvice?.({ type: "session", title: out.title, summary: `${out.title} — ${out.why || out.meta}` });
      setTurns((t) => [...t, {
        role: "app",
        result: {
          kind: "coach", why: out.why, note: out.coachNote,
          wod: { title: out.title, format: out.format || "AMRAP", meta: out.meta, difficulty: "Coach", tempo: out.tempo || "none", movements: moves, timerCfg: { total: (intentRef.current?.minutes || 15) * 60 }, fromTalk: true },
        },
      }]);
    } catch (e) {
      setTurns((t) => [...t, { role: "app", result: { kind: "note", say: "The AI coach couldn't be reached from here — it only works where the app can talk to Claude. Everything above was built on your device and works offline." } }]);
    }
    setCoachBusy(false);
  };

  const lastAppIdx = turns.map((t) => t.role).lastIndexOf("app");
  const lastIsWorkout = turns[lastAppIdx]?.result?.kind === "workout";

  return (
    <div style={{ background: T.ink, minHeight: "100vh", paddingBottom: 200 }}>
      <TopBar title="Build it" accent={T.ember}
        right={turns.length > 0 && (
          <button onClick={startOver} className="text-xs px-3 py-1.5 rounded-full" style={{ background: T.surface2, color: T.muted }}>Start over</button>
        )} />

      <div className="px-4">
        {turns.length === 0 && (
          <div className="pt-2">
            <p style={{ color: T.chalk, fontSize: 22, fontWeight: 800, lineHeight: 1.2 }}>What do you want to do?</p>
            <p className="text-[13.5px] mt-1.5" style={{ color: T.chalkDim, lineHeight: 1.5 }}>
              Say it how you'd say it to a coach: how long, what to work, what you've got, and anything to avoid.
              {speech.supported ? " Tap the mic to talk." : ""}
            </p>
            <button onClick={openEquipment} className="mt-3 text-[12px] flex items-center gap-1.5" style={{ color: T.muted }}>
              <Settings2 size={13} /> Using your saved kit ({availEquip.length}) unless you name some
            </button>

            {recent.length > 0 && (
              <>
                <p className="mt-5 mb-2" style={{ ...display, color: T.muted, fontSize: 10, letterSpacing: "0.16em" }}>YOUR RECENT ASKS</p>
                <div className="flex flex-col gap-1.5">
                  {recent.map((r) => (
                    <button key={r} onClick={() => respond(r, "text")} className="text-left px-3.5 py-2.5 rounded-xl text-[13px]"
                      style={{ background: T.surface, color: T.chalk, border: `1px solid ${T.line}` }}>{r}</button>
                  ))}
                </div>
              </>
            )}

            <p className="mt-5 mb-2" style={{ ...display, color: T.muted, fontSize: 10, letterSpacing: "0.16em" }}>TRY</p>
            <div className="flex flex-wrap gap-1.5">
              {TALK_EXAMPLES.map((ex) => (
                <button key={ex} onClick={() => respond(ex, "text")} className="px-3 py-2 rounded-full text-[12.5px]"
                  style={{ background: T.surface, color: T.chalkDim, border: `1px solid ${T.line}` }}>{ex}</button>
              ))}
            </div>
          </div>
        )}

        <div className="flex flex-col gap-3 pt-1">
          {turns.map((t, i) => t.role === "user" ? (
            <div key={i} ref={i === turns.map((x) => x.role).lastIndexOf("user") ? lastUserRef : null}
              className="self-end max-w-[85%] px-3.5 py-2.5 rounded-2xl rounded-br-md text-[14px] flex items-start gap-1.5"
              style={{ background: T.chalk, color: T.surface, scrollMarginTop: 84 }}>
              {t.via === "voice" && <Mic size={13} className="mt-0.5 shrink-0" style={{ opacity: 0.7 }} />}
              <span>{t.text}</span>
            </div>
          ) : (
            <TalkReply key={i} turn={t} latest={i === lastAppIdx}
              onStart={(w) => { Sound.stopSpeech(); onLoadWod(w); }}
              onRefine={(txt) => respond(txt, "text")}
              onOpenYoga={onOpenYoga} onOpenBreath={onOpenBreath} />
          ))}
          {coachBusy && <p className="text-sm text-center py-3" style={{ color: T.violet }}>Asking the coach…</p>}
          {lastIsWorkout && !coachBusy && (
            <button onClick={askAiCoach} className="self-center text-[12px] flex items-center gap-1.5 px-3 py-1.5 rounded-full"
              style={{ color: T.violetInk, background: T.violetDim }}>
              <Sparkles size={12} /> Ask the AI coach to design it instead
            </button>
          )}
          <div ref={endRef} style={{ scrollMarginBottom: 180 }} />
        </div>
      </div>

      {/* Composer. Sits above the nav bar and its raised hub. */}
      <div className="fixed left-0 right-0 z-20 px-3" style={{ bottom: 104 }}>
        {speech.error && (
          <button onClick={speech.clearError} className="w-full text-left text-[12px] px-3 py-2 mb-1.5 rounded-xl"
            style={{ background: T.amberDim, color: T.amberInk }}>{speech.error}</button>
        )}
        <form onSubmit={(e) => { e.preventDefault(); send(); }}
          className="flex items-end gap-2 p-1.5 rounded-[26px]"
          style={{ background: T.surface, border: `1px solid ${speech.listening ? T.ember : T.line}`, boxShadow: "0 6px 24px rgba(22,24,29,0.12)" }}>
          <textarea ref={inputRef} value={draft} rows={1} aria-label="Describe your workout"
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={(e) => { if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(); } }}
            placeholder={speech.listening ? "Listening…" : turns.length ? "Harder, 30 min, no burpees…" : "e.g. 25 min upper body, no jumping"}
            className="flex-1 resize-none bg-transparent px-3 py-2.5 text-[15px] outline-none"
            style={{ color: T.chalk, maxHeight: 120 }} />
          {speech.supported && (
            <button type="button" aria-label={speech.listening ? "Stop listening" : "Speak your request"}
              onClick={speech.listening ? speech.stop : speech.start}
              className="w-11 h-11 rounded-full flex items-center justify-center shrink-0"
              style={{
                background: speech.listening ? T.ember : T.emberDim,
                boxShadow: speech.listening ? `0 0 0 6px ${T.ember}22` : "none",
                transition: "box-shadow 200ms ease",
              }}>
              <Mic size={19} color={speech.listening ? T.onAccent : T.emberInk} />
            </button>
          )}
          <button type="submit" aria-label="Build workout" disabled={!draft.trim()}
            className="w-11 h-11 rounded-full flex items-center justify-center shrink-0"
            style={{ background: draft.trim() ? T.chalk : T.surface2 }}>
            <Send size={17} color={draft.trim() ? T.surface : T.muted} />
          </button>
        </form>
      </div>
    </div>
  );
}

const NAV_ITEMS = [
  // Inner circle: the four that sit in the bar. Home first, and the rest
  // weighted toward wellbeing rather than putting a workout in front of you
  // before the app knows anything about your day.
  { id: "home", label: "Home", icon: HomeIcon, color: T.ember, ring: 0 },
  { id: "coach", label: "Coach", icon: Sparkles, color: T.violet, ring: 0 },
  { id: "yoga", label: "Yoga", icon: Wind, color: T.sage, ring: 0 },
  { id: "progress", label: "You", icon: TrendingUp, color: T.amber, ring: 0 },
  // Outer circle: everything else, one tap away in the hub.
  { id: "talk", label: "Build it", icon: Mic, color: T.ember, ring: 1 },
  { id: "generator", label: "Workouts", icon: Dumbbell, color: T.ember, ring: 1 },
  { id: "muscle", label: "Lifts", icon: Target, color: T.ember, ring: 1 },
  { id: "cardio", label: "Cardio", icon: HeartPulse, color: T.cyan, ring: 1 },
  { id: "breath", label: "Breath", icon: Wind, color: T.breath, ring: 1 },
  { id: "plan", label: "Plan", icon: CalendarDays, color: T.amber, ring: 1 },
  { id: "favorites", label: "Saved", icon: Star, color: T.amber, ring: 1 },
  { id: "tools", label: "Tools", icon: Wrench, color: T.amber, ring: 1 },
  { id: "search", label: "Search", icon: Search, color: T.muted, ring: 1 },
];

function RadialNav({ tab, setTab, store, onQuickLog, onCheckIn }) {
  const [open, setOpen] = useState(false);
  const [pressed, setPressed] = useState(null);

  const rings = [0, 1].map((r) => NAV_ITEMS.filter((i) => i.ring === r));
  const RADII = [88, 158];

  // Centred in the viewport, the menu gets a full 360deg instead of the half
  // arc a bottom-anchored hub allows — so the same twelve destinations sit
  // further apart and read as two clean concentric rings. The outer ring is
  // rotated half a step so its petals fall in the gaps of the inner one.
  const place = (idx, count, radius, offsetDeg) => {
    const angle = -90 + offsetDeg + (360 / count) * idx;
    const rad = (angle * Math.PI) / 180;
    return { x: Math.cos(rad) * radius, y: Math.sin(rad) * radius };
  };

  const go = (id) => { setTab(id); setOpen(false); };

  // Contextual shortcuts: what you most likely came here to do right now.
  const todayIdx = (new Date().getDay() + 6) % 7;
  const todayPlan = store.plan ? store.plan.days[todayIdx] : null;
  const checkedIn = (store.metrics || []).some((m) => m.day === new Date().toISOString().slice(0, 10));

  return (
    <>
      {/* Scrim */}
      {open && (
        <div
          onClick={() => setOpen(false)}
          className="fixed inset-0 z-40"
          style={{
            background: "radial-gradient(circle at 50% 50%, rgba(255,255,255,0.55), rgba(22,24,29,0.55) 70%)",
            backdropFilter: "blur(6px)",
          }}
        />
      )}

      {/* Petals */}
      {open && (
        <div className="fixed z-40" style={{ left: "50%", top: "50%", width: 0, height: 0 }}>
          {rings.map((items, ringIdx) =>
            items.map((item, i) => {
              const { x, y } = place(i, items.length, RADII[ringIdx], ringIdx ? 360 / (items.length * 2) : 0);
              const Icon = item.icon;
              const active = tab === item.id;
              const delay = ringIdx * 50 + i * 22;
              return (
                <button
                  key={item.id}
                  onClick={() => go(item.id)}
                  onTouchStart={() => setPressed(item.id)}
                  onTouchEnd={() => setPressed(null)}
                  className="absolute flex flex-col items-center justify-center rounded-full"
                  style={{
                    width: 64, height: 64,
                    marginLeft: -32, marginTop: -32,
                    transform: `translate(${x.toFixed(1)}px, ${y.toFixed(1)}px) scale(${pressed === item.id ? 0.9 : 1})`,
                    background: active ? item.color : T.surface,
                    border: `1.5px solid ${active ? item.color : item.color + "55"}`,
                    boxShadow: active ? `0 0 18px ${item.color}66` : "0 2px 10px rgba(0,0,0,0.4)",
                    animation: `petalIn 280ms cubic-bezier(.2,1.2,.4,1) ${delay}ms both`,
                    transition: "transform 90ms ease",
                  }}
                >
                  <Icon size={19} color={active ? T.ink : item.color} />
                  <span className="text-[9px] mt-0.5" style={{ color: active ? T.onAccent : T.chalk, fontWeight: active ? 700 : 500 }}>
                    {item.label}
                  </span>
                </button>
              );
            })
          )}

          {/* Close control sits at the centre, where the hub visually "moved" to */}
          <button
            onClick={() => setOpen(false)}
            className="absolute flex items-center justify-center rounded-full"
            style={{
              width: 62, height: 62, marginLeft: -31, marginTop: -31,
              background: T.chalk,
              boxShadow: "0 4px 16px rgba(22,24,29,0.3)",
              animation: "petalIn 200ms ease both",
            }}
          >
            <span style={{ display: "inline-flex", transform: "rotate(135deg)" }}>
              <Plus size={28} color={T.chalk} strokeWidth={2.5} />
            </span>
          </button>

          {/* Contextual row above the arcs */}
          <div className="absolute flex gap-2 whitespace-nowrap"
            style={{ transform: "translate(-50%, 208px)", left: 0, animation: "petalIn 300ms ease 260ms both" }}>
            <button onClick={() => { onQuickLog(); setOpen(false); }}
              className="px-3 py-2 rounded-full flex items-center gap-1.5"
              style={{ background: T.ember, color: T.ink, fontSize: 12, fontWeight: 700 }}>
              <Check size={13} /> Log a session
            </button>
            {!checkedIn && (
              <button onClick={() => { onCheckIn(); setOpen(false); }}
                className="px-3 py-2 rounded-full flex items-center gap-1.5"
                style={{ background: T.surface2, color: T.chalk, fontSize: 12, fontWeight: 600 }}>
                <Plus size={13} /> Check in
              </button>
            )}
          </div>

          {todayPlan && (
            <div className="absolute text-center" style={{ transform: "translate(-50%, 256px)", left: 0, animation: "petalIn 300ms ease 300ms both" }}>
              <span className="text-[11px] whitespace-nowrap" style={{ color: T.muted }}>
                Today's plan: <span style={{ color: T.amber }}>{todayPlan}</span>
              </span>
            </div>
          )}
        </div>
      )}

      <style>{`
        @keyframes petalIn {
          from { opacity: 0; transform: translate(0px, 0px) scale(0.4); }
        }
        @keyframes hubSpin { to { transform: rotate(135deg); } }
      `}</style>

      {/* Bar: two quick tabs either side of the hub */}
      <div className="fixed bottom-0 left-0 right-0 z-30 flex items-end justify-around pb-3 pt-2"
        style={{ background: T.surface, borderTop: `1px solid ${T.line}`, boxShadow: "0 -2px 14px rgba(22,24,29,0.06)" }}>
        {[NAV_ITEMS[0], NAV_ITEMS[1]].map((t) => {
          const Icon = t.icon;
          const active = tab === t.id && !open;
          return (
            <button key={t.id} onClick={() => { setOpen(false); setTab(t.id); }} className="flex flex-col items-center gap-1 w-16">
              <Icon size={19} color={active ? t.color : T.muted} />
              <span className="text-[10px]" style={{ color: active ? t.color : T.muted, fontWeight: active ? 700 : 500 }}>{t.label}</span>
            </button>
          );
        })}

        {/* Hub */}
        <button
          onClick={() => setOpen((o) => !o)}
          aria-label="Open navigation"
          className="flex items-center justify-center rounded-full"
          style={{
            width: 76, height: 76, marginTop: -34,
            background: T.ember,
            border: `3px solid ${T.surface}`,
            boxShadow: `0 6px 22px ${T.ember}66`,
            // Hides as the menu opens, so it reads as having moved to the centre.
            opacity: open ? 0 : 1,
            transform: open ? "scale(0.5)" : "scale(1)",
            transition: "opacity 180ms ease, transform 240ms cubic-bezier(.2,1.2,.4,1)",
            pointerEvents: open ? "none" : "auto",
          }}
        >
          <Plus size={34} color={T.onAccent} strokeWidth={2.5} />
        </button>

        {[NAV_ITEMS[2], NAV_ITEMS[3]].map((t) => {
          const Icon = t.icon;
          const active = tab === t.id && !open;
          return (
            <button key={t.id} onClick={() => { setOpen(false); setTab(t.id); }} className="flex flex-col items-center gap-1 w-16">
              <Icon size={19} color={active ? t.color : T.muted} />
              <span className="text-[10px]" style={{ color: active ? t.color : T.muted, fontWeight: active ? 700 : 500 }}>{t.label}</span>
            </button>
          );
        })}
      </div>
    </>
  );
}

/* Browsers refuse to start audio outside a user gesture, so the first cue of a
   session would otherwise be silently swallowed. One listener on the first tap
   is enough to unlock playback for the rest of the visit. */
function AudioUnlock() {
  useEffect(() => {
    const unlock = () => { Sound.ready(); };
    window.addEventListener("pointerdown", unlock, { once: true });
    window.addEventListener("touchstart", unlock, { once: true });
    return () => {
      window.removeEventListener("pointerdown", unlock);
      window.removeEventListener("touchstart", unlock);
    };
  }, []);
  return null;
}

/* ============================== ERROR BOUNDARY ==============================
   Without this, one bad render — a malformed coach response, a corrupt saved
   workout, a field that turned out to be undefined — white-screens the whole
   app with no way back. A boundary turns that into a recoverable message, and
   critically offers a reset that clears only the volatile state rather than
   the training history someone has spent weeks building.
==================================================================== */
class ErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { failed: false, detail: "" };
  }
  static getDerivedStateFromError(err) {
    return { failed: true, detail: String(err?.message || err) };
  }
  componentDidCatch(err, info) {
    // Kept in the console rather than shown: the message helps a developer,
    // a stack trace helps nobody standing in a gym.
    console.error("Render failed:", err, info);
  }
  render() {
    if (!this.state.failed) return this.props.children;
    const T2 = this.props.theme;
    return (
      <div style={{ background: T2.bg, minHeight: "100vh", padding: "56px 24px",
                    fontFamily: "system-ui, -apple-system, sans-serif" }}>
        <h1 style={{ color: T2.ink, fontSize: 24, fontWeight: 800, margin: 0 }}>
          Something broke
        </h1>
        <p style={{ color: T2.ink2, fontSize: 14, lineHeight: 1.55, marginTop: 10 }}>
          That's on the app, not on you. Your logged sessions are safe — reloading
          won't lose them.
        </p>
        <button
          onClick={() => window.location.reload()}
          style={{ marginTop: 22, width: "100%", padding: "14px", borderRadius: 999,
                   background: T2.ink, color: "#fff", border: "none", fontSize: 15, fontWeight: 700 }}>
          Reload
        </button>
        <p style={{ color: T2.ink3, fontSize: 11, marginTop: 18, wordBreak: "break-word" }}>
          {this.state.detail}
        </p>
      </div>
    );
  }
}

/* ============================== APP ROOT ============================== */
function AppInner() {
  const store = useStore();
  const [tab, setTab] = useState("home");
  const [equipOpen, setEquipOpen] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [logOpen, setLogOpen] = useState(false);
  const [logPreset, setLogPreset] = useState(null);
  const [builderOpen, setBuilderOpen] = useState(false);
  const [injectedWod, setInjectedWod] = useState(null);
  const [jumpExercise, setJumpExercise] = useState(null);
  const [jumpYogaArea, setJumpYogaArea] = useState(null);
  const [metricsOpen, setMetricsOpen] = useState(false);
  const [quickStart, setQuickStart] = useState(false);
  const [jumpBreath, setJumpBreath] = useState(null);

  const openLog = (preset) => { setLogPreset(preset); setLogOpen(true); };

  if (!store.loaded) {
    return <div style={{ background: T.ink, minHeight: "100vh" }} />;
  }
  if (!store.settings.seenIntro) {
    return (
      <FirstRun
        store={store}
        onDone={() => {
          // Always land on Home. Opening straight into a generated workout
          // undercuts the point of a wellbeing-first app — Home already offers
          // the coach's read, today's session and three things to try, so
          // starting a workout stays a choice rather than the default.
          setTab("home");
        }}
      />
    );
  }
  if (builderOpen) {
    return (
      <BuilderScreen
        store={store}
        onBack={() => setBuilderOpen(false)}
        onLaunch={(wod) => { setInjectedWod(wod); setBuilderOpen(false); setTab("generator"); }}
      />
    );
  }

  return (
    <div style={{ ...chalkboardBg, minHeight: "100vh", fontFamily: "system-ui, -apple-system, sans-serif" }}>
      <style>{ICON_CSS}</style>
      <AudioUnlock />
      {tab === "home" && (
        <HomeScreen
          setTab={setTab} store={store}
          openEquipment={() => setEquipOpen(true)}
          openSettings={() => setSettingsOpen(true)}
          openBuilder={() => setBuilderOpen(true)}
          onJustStart={() => { setQuickStart(true); setTab("generator"); }}
          onOpenMovement={(ex) => { setJumpExercise(ex); setTab("muscle"); }}
          onOpenYoga={(area) => { setJumpYogaArea(area); setTab("yoga"); }}
          onTalk={() => setTab("talk")}
        />
      )}
      {tab === "talk" && (
        <TalkScreen
          store={store}
          openEquipment={() => setEquipOpen(true)}
          onLoadWod={(wod) => { setInjectedWod(wod); setTab("generator"); }}
          onOpenYoga={(area) => { setJumpYogaArea(area); setTab("yoga"); }}
          onOpenBreath={(id) => { setJumpBreath(id || null); setTab("breath"); }}
        />
      )}
      {tab === "generator" && (
        <GeneratorScreen
          store={store}
          openEquipment={() => setEquipOpen(true)}
          openBuilder={() => setBuilderOpen(true)}
          injectedWod={injectedWod}
          clearInjected={() => setInjectedWod(null)}
          onLog={openLog}
          autoGenerate={quickStart}
          clearAutoGenerate={() => setQuickStart(false)}
        />
      )}
      {tab === "muscle" && <MuscleScreen store={store} jumpExercise={jumpExercise} clearJump={() => setJumpExercise(null)} onLog={openLog} />}
      {tab === "cardio" && <CardioScreen store={store} onLog={openLog} />}
      {tab === "yoga" && <YogaScreen store={store} jumpArea={jumpYogaArea} clearJumpArea={() => setJumpYogaArea(null)} />}
      {tab === "breath" && <BreathScreen store={store} onLog={openLog} jumpPattern={jumpBreath} clearJumpPattern={() => setJumpBreath(null)} />}
      {tab === "favorites" && (
        <FavoritesScreen
          store={store}
          onOpenExercise={(e) => { setJumpExercise(e); setTab("muscle"); }}
          onOpenYogaArea={(a) => { setJumpYogaArea(a); setTab("yoga"); }}
          onLoadWod={(w) => { setInjectedWod(w); setTab("generator"); }}
        />
      )}
      {tab === "progress" && <ProgressScreen store={store} onOpenMetrics={() => setMetricsOpen(true)} />}
      {tab === "coach" && (
        <CoachScreen
          store={store}
          onLog={openLog}
          onLoadWod={(wod) => { setInjectedWod(wod); setTab("generator"); }}
          onOpenYoga={(area) => { setJumpYogaArea(area); setTab("yoga"); }}
          onOpenBreath={() => setTab("breath")}
        />
      )}
      {tab === "plan" && <PlanScreen store={store} />}
      {tab === "tools" && <ToolsScreen store={store} openBuilder={() => setBuilderOpen(true)} onLog={openLog} />}
      {tab === "search" && (
        <SearchScreen
          store={store}
          onOpenExercise={(e) => { setJumpExercise(e); setTab("muscle"); }}
          onOpenPose={() => setTab("yoga")}
        />
      )}

      <EquipmentModal open={equipOpen} onClose={() => setEquipOpen(false)} equipment={store.equipment} setEquipment={store.setEquipment} />
      <SettingsModal open={settingsOpen} onClose={() => setSettingsOpen(false)} store={store} />
      <LogModal open={logOpen} onClose={() => setLogOpen(false)} store={store} preset={logPreset} />
      <MetricsModal open={metricsOpen} onClose={() => setMetricsOpen(false)} store={store} />

      <RadialNav
        tab={tab}
        setTab={setTab}
        store={store}
        onQuickLog={() => openLog(null)}
        onCheckIn={() => setMetricsOpen(true)}
      />
    </div>
  );
}

export default function App() {
  return (
    <ErrorBoundary theme={{ bg: T.ink, ink: T.chalk, ink2: T.chalkDim, ink3: T.muted }}>
      <AppInner />
    </ErrorBoundary>
  );
}
