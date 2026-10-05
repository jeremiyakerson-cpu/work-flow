const test = require("node:test");
const assert = require("node:assert/strict");
const { load, makeStorage } = require("./load");

const fresh = (storage) => load(["ekg-stats.js"], { localStorage: storage || makeStorage() }).EkgStats;

test("profile aggregates by category, rhythm and focus", () => {
  const S = fresh();
  S.record({ kind: "quiz", category: "rhythm", rhythm: "afib", focus: null, correct: true });
  S.record({ kind: "quiz", category: "rhythm", rhythm: "afib", focus: null, correct: false });
  S.record({ kind: "scenario", category: null, rhythm: "vt", focus: "tachycardia", correct: true });
  const p = S.profile();
  assert.equal(p.total, 3);
  assert.equal(p.correct, 2);
  assert.deepEqual({ ...p.byCategory.rhythm }, { correct: 1, total: 2, accuracy: 0.5 });
  assert.equal(p.byRhythm.afib.total, 2);
  assert.equal(p.byFocus.tachycardia.accuracy, 1);
  assert.equal(p.byCategory.null, undefined, "null keys are not bucketed");
});

test("rolling window keeps only the most recent 400 events", () => {
  const S = fresh();
  for (let i = 0; i < 450; i++) S.record({ category: "meds", correct: i >= 50 });
  const p = S.profile();
  assert.equal(p.total, 400);
  assert.equal(p.correct, 400, "the 50 oldest (incorrect) answers were dropped");
});

test("weakness: unseen = neutral 0.5, evidence pulls toward miss rate", () => {
  const S = fresh();
  for (let i = 0; i < 6; i++) S.record({ focus: "bradycardia", correct: false });
  for (let i = 0; i < 6; i++) S.record({ focus: "tachycardia", correct: true });
  const w = S.weakness(["bradycardia", "tachycardia", "torsades-qt"]);
  assert.equal(w.bradycardia, 1);
  assert.equal(w.tachycardia, 0);
  assert.equal(w["torsades-qt"], 0.5);
});

test("summaryLine needs 5 answers, then reports category accuracy", () => {
  const S = fresh();
  for (let i = 0; i < 4; i++) S.record({ category: "code", correct: true });
  assert.equal(S.summaryLine(), null);
  S.record({ category: "code", correct: false });
  assert.match(S.summaryLine(), /Code 80%.*5 answers tracked/);
});

test("corrupt or foreign storage is treated as empty, reset clears", () => {
  const storage = makeStorage({ "ekg-zoll-trainer-stats-v1": "{not json" });
  const S = fresh(storage);
  assert.equal(S.profile().total, 0);
  S.record({ category: "meds", correct: true });
  assert.equal(S.profile().total, 1);
  S.reset();
  assert.equal(S.profile().total, 0);
});

test("storage that throws (private mode / quota) never breaks recording", () => {
  const throwing = {
    getItem() {
      throw new Error("denied");
    },
    setItem() {
      throw new Error("quota");
    },
    removeItem() {
      throw new Error("denied");
    },
  };
  const S = fresh(throwing);
  assert.doesNotThrow(() => S.record({ category: "meds", correct: true }));
  assert.equal(S.profile().total, 0);
  assert.doesNotThrow(() => S.reset());
});
