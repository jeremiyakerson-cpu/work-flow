// Structural checks over the content banks. These guard against typos
// that the app would otherwise swallow silently (an unknown rhythm key
// quietly renders NSR; an out-of-range answer index makes a question
// unanswerable).
const test = require("node:test");
const assert = require("node:assert/strict");
const vm = require("vm");
const { load } = require("./load");
const { validateScenario } = require("./validate");

const ctx = load(["ekg-rhythms.js", "ekg-questions.js", "ekg-scenarios.js", "ekg-education.js"]);
const RHYTHM_KEYS = Object.keys(vm.runInContext("RHYTHMS", ctx));
const { EKG_QUESTIONS, EKG_SCENARIOS, EkgEducation, EkgRhythms } = ctx;

test("every rhythm synthesizes a finite strip of the expected length", () => {
  for (const key of RHYTHM_KEYS) {
    const strip = EkgRhythms.synthesizeRhythm(key);
    assert.equal(strip.length, EkgRhythms.SAMPLE_COUNT, key);
    assert.ok(Array.from(strip).every(Number.isFinite), `${key} has non-finite samples`);
  }
});

test("question bank: ids unique, categories known, answers in range, rhythms exist", () => {
  const ids = new Set();
  for (const q of EKG_QUESTIONS) {
    const where = `question ${q.id}`;
    assert.ok(!ids.has(q.id), `duplicate id ${q.id}`);
    ids.add(q.id);
    assert.ok(["rhythm", "meds", "condition", "code"].includes(q.category), `${where}: category ${q.category}`);
    assert.ok(RHYTHM_KEYS.includes(q.rhythm), `${where}: unknown rhythm ${q.rhythm}`);
    assert.ok(Array.isArray(q.choices) && q.choices.length >= 2, `${where}: choices`);
    assert.equal(new Set(q.choices).size, q.choices.length, `${where}: duplicate choices`);
    assert.ok(Number.isInteger(q.answer) && q.answer >= 0 && q.answer < q.choices.length, `${where}: answer index`);
    assert.ok(q.stem && q.rationale, `${where}: stem/rationale`);
  }
});

test("question bank is large enough for the longest mode (exam = 40)", () => {
  assert.ok(EKG_QUESTIONS.length >= 40, `only ${EKG_QUESTIONS.length} questions`);
});

test("authored scenarios are structurally valid with unique ids", () => {
  const ids = new Set();
  for (const s of EKG_SCENARIOS) {
    assert.ok(!ids.has(s.id), `duplicate scenario id ${s.id}`);
    ids.add(s.id);
    validateScenario(s, RHYTHM_KEYS);
  }
});

test("rhythm guide entries reference real rhythms and groups", () => {
  const { RHYTHM_GUIDE, RHYTHM_GROUPS, SPRINT_CONFUSION, MED_GUIDE } = EkgEducation;
  for (const [key, g] of Object.entries(RHYTHM_GUIDE)) {
    assert.ok(RHYTHM_KEYS.includes(key), `guide key ${key} has no waveform`);
    assert.ok(RHYTHM_GROUPS.includes(g.group), `${key}: group ${g.group}`);
    assert.ok(g.name && g.demo && g.criteria, `${key}: missing fields`);
    for (const f of ["rate", "regular", "p", "pr", "qrs"]) assert.ok(g.criteria[f], `${key}: criteria.${f}`);
    assert.ok(Array.isArray(g.causes) && Array.isArray(g.confuse), `${key}: causes/confuse`);
  }
  for (const group of SPRINT_CONFUSION) {
    for (const key of group) assert.ok(RHYTHM_GUIDE[key], `confusion key ${key} not in guide`);
  }
  // the sprint needs at least 4 distinct names to build a choice set
  assert.ok(Object.keys(RHYTHM_GUIDE).length >= 4);
  for (const m of MED_GUIDE) {
    for (const f of ["name", "cls", "dose", "use", "caution", "pearl"]) assert.ok(m[f], `${m.name}: ${f}`);
  }
});
