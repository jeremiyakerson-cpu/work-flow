const test = require("node:test");
const assert = require("node:assert/strict");
const { load, makeStorage } = require("./load");

const entry = (over = {}) => ({
  title: "Case",
  scenarioId: "x",
  focus: "bradycardia",
  outcome: "completed",
  correct: 4,
  answered: 5,
  total: 5,
  durationMs: 61000,
  misses: [{ question: "Q?", intervention: "i", rationale: "First sentence. Second one.", critical: true }],
  ...over,
});

test("code log: newest first, capped at 50", () => {
  const { EkgCodeLog: L } = load(["ekg-codelog.js"]);
  for (let i = 0; i < 55; i++) L.add(entry({ title: `run ${i}` }));
  assert.equal(L.count(), 50);
  assert.equal(L.list()[0].title, "run 54");
});

test("code log: pointers use the first sentence of the rationale", () => {
  const { EkgCodeLog: L } = load(["ekg-codelog.js"]);
  const [p] = L.pointersFor(entry());
  assert.equal(p.text, "Q? → First sentence.");
  assert.equal(p.critical, true);
  assert.deepEqual([...L.pointersFor({})], []);
});

test("code log: patterns report deaths, weakest focus, and flawless streaks", () => {
  const { EkgCodeLog: L } = load(["ekg-codelog.js"]);
  assert.deepEqual([...L.patterns()], []);
  L.add(entry({ outcome: "died" }));
  L.add(entry({ focus: "tachycardia", misses: [] }));
  const pats = L.patterns();
  assert.ok(pats.some((p) => /1 patient lost across 2 logged runs — most often in bradycardia/.test(p)), pats.join("\n"));
  assert.ok(pats.some((p) => /Most missed decisions: bradycardia \(1\)/.test(p)));

  const { EkgCodeLog: L2 } = load(["ekg-codelog.js"]);
  for (let i = 0; i < 3; i++) L2.add(entry({ misses: [] }));
  const p2 = L2.patterns();
  assert.ok(p2.some((p) => /No missed decisions/.test(p)));
  assert.ok(p2.some((p) => /Last 3 runs flawless/.test(p)));
});

test("exam history tolerates corrupt storage", () => {
  const storage = makeStorage({ "ekg-exam-history-v1": "{bad" });
  const ctx = load(["ekg-scenarios.js", "ekg-education.js", "ekg-stats.js", "ekg-progress.js"], { localStorage: storage });
  assert.deepEqual([...ctx.EkgProgress.examHistory()], []);
  storage.setItem("ekg-exam-history-v1", JSON.stringify([{ t: 1, score: 32, total: 40, pct: 80, pass: true }]));
  assert.equal(ctx.EkgProgress.examHistory().length, 1);
});
