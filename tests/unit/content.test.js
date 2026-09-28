// Data-driven checks over every content bank. Each rhythm, question,
// scenario and guide entry gets its own subtest, so new content added to
// the data files is covered automatically and a failure names the item.
// These guard against typos the app would otherwise swallow silently (an
// unknown rhythm key quietly renders NSR; an out-of-range answer index
// makes a question unanswerable).
const test = require("node:test");
const assert = require("node:assert/strict");
const vm = require("vm");
const { load } = require("./load");
const { validateScenario, validateChoices, validateVitals, hasMarkup } = require("./validate");

const ctx = load(["ekg-rhythms.js", "ekg-questions.js", "ekg-scenarios.js", "ekg-education.js"]);
const RHYTHM_KEYS = Object.keys(vm.runInContext("RHYTHMS", ctx));
const { EKG_QUESTIONS, EKG_SCENARIOS, EkgEducation, EkgRhythms } = ctx;
const { RHYTHM_GUIDE, RHYTHM_GROUPS, SPRINT_CONFUSION, MED_GUIDE } = EkgEducation;
const CATEGORIES = ["rhythm", "meds", "condition", "code"];

// Rhythms whose strip is (near-)flat by design.
const FLATLINE = new Set(["asystole"]);

test("every rhythm generates a valid waveform", async (t) => {
  assert.ok(RHYTHM_KEYS.length > 0);
  for (const key of RHYTHM_KEYS) {
    await t.test(key, () => {
      // several draws: many rhythms randomize their beat timing/morphology
      for (let run = 0; run < 5; run++) {
        const strip = EkgRhythms.synthesizeRhythm(key);
        assert.equal(strip.length, EkgRhythms.SAMPLE_COUNT);
        let min = Infinity;
        let max = -Infinity;
        for (const v of strip) {
          assert.ok(Number.isFinite(v), "non-finite sample");
          if (v < min) min = v;
          if (v > max) max = v;
        }
        // the monitor scales for roughly -1.3..1.3; far outside clips off-screen
        assert.ok(min >= -2 && max <= 2, `amplitude out of range: ${min.toFixed(2)}..${max.toFixed(2)}`);
        if (FLATLINE.has(key)) assert.ok(max - min < 0.2, "flatline rhythm has visible complexes");
        else assert.ok(max - min > 0.3, `strip is nearly flat (peak-to-peak ${(max - min).toFixed(3)})`);
      }
    });
  }
});

test("every question is answerable: one correct choice, a rationale, a real rhythm", async (t) => {
  const ids = new Set();
  for (const q of EKG_QUESTIONS) {
    await t.test(`question ${q.id}`, () => {
      assert.ok(q.id !== undefined && !ids.has(q.id), `missing or duplicate id ${q.id}`);
      ids.add(q.id);
      assert.ok(CATEGORIES.includes(q.category), `category ${q.category}`);
      assert.ok(RHYTHM_KEYS.includes(q.rhythm), `unknown rhythm ${q.rhythm}`);
      assert.ok(typeof q.stem === "string" && q.stem.trim(), "stem");
      assert.ok(typeof q.rationale === "string" && q.rationale.trim(), "rationale");
      validateChoices(q.choices, q.answer);
      validateVitals(q.vitals);
      if ("shockable" in q) {
        assert.equal(q.category, "code", "shockable is only meaningful on code questions");
        assert.equal(typeof q.shockable, "boolean");
      }
      for (const s of [q.stem, q.rationale, ...q.choices]) assert.ok(!hasMarkup(s), `HTML-like text: ${s}`);
    });
  }
});

test("question bank is large enough for the longest mode (exam = 40)", () => {
  assert.ok(EKG_QUESTIONS.length >= 40, `only ${EKG_QUESTIONS.length} questions`);
});

test("every authored scenario is valid and references existing rhythms", async (t) => {
  const ids = new Set();
  for (const s of EKG_SCENARIOS) {
    await t.test(`scenario ${s.id}`, () => {
      assert.ok(!ids.has(s.id), `duplicate scenario id ${s.id}`);
      ids.add(s.id);
      assert.ok(!String(s.id).startsWith("gen-"), "gen- ids are reserved for generated cases");
      validateScenario(s, RHYTHM_KEYS);
    });
  }
});

test("every rhythm guide entry is complete and points at a real waveform", async (t) => {
  const names = new Map();
  for (const [key, g] of Object.entries(RHYTHM_GUIDE)) {
    await t.test(key, () => {
      assert.ok(RHYTHM_KEYS.includes(key), `guide key ${key} has no waveform`);
      assert.ok(RHYTHM_GROUPS.includes(g.group), `group ${g.group}`);
      assert.ok(g.name && g.demo && g.criteria, "missing fields");
      for (const f of ["rate", "regular", "p", "pr", "qrs"]) assert.ok(g.criteria[f], `criteria.${f}`);
      assert.ok(Array.isArray(g.causes) && Array.isArray(g.confuse), "causes/confuse");
      // the sprint grades by display name, so two rhythms can't share one
      assert.ok(!names.has(g.name), `name "${g.name}" also used by ${names.get(g.name)}`);
      names.set(g.name, key);
    });
  }
  // the sprint needs at least 4 distinct names to build a choice set
  assert.ok(names.size >= 4);
});

test("every rhythm group in the study guide has at least one rhythm", () => {
  const used = new Set(Object.values(RHYTHM_GUIDE).map((g) => g.group));
  for (const group of RHYTHM_GROUPS) assert.ok(used.has(group), `group ${group} is empty`);
});

test("sprint confusion groups only name rhythms in the guide", () => {
  for (const group of SPRINT_CONFUSION) {
    for (const key of group) assert.ok(RHYTHM_GUIDE[key], `confusion key ${key} not in guide`);
  }
});

test("every rhythm shown in a question or scenario can be studied", () => {
  const shown = new Set([...EKG_QUESTIONS.map((q) => q.rhythm), ...EKG_SCENARIOS.flatMap((s) => s.stages.map((st) => st.rhythm))]);
  const missing = [...shown].filter((k) => !RHYTHM_GUIDE[k]);
  assert.deepEqual(missing, [], "rhythms learners meet with no study-guide entry");
});

test("every medication card is complete", async (t) => {
  const names = new Set();
  for (const m of MED_GUIDE) {
    await t.test(m.name || "(unnamed)", () => {
      for (const f of ["name", "cls", "dose", "use", "caution", "pearl"]) assert.ok(m[f], f);
      assert.ok(!names.has(m.name), "duplicate medication card");
      names.add(m.name);
    });
  }
});
