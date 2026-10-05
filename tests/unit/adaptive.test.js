const test = require("node:test");
const assert = require("node:assert/strict");
const { load, makeStorage } = require("./load");

const FILES = ["ekg-education.js", "ekg-questions.js", "ekg-stats.js", "ekg-adaptive.js"];
const MIN = 60 * 1000;
const DAY = 24 * 60 * MIN;
const NOW = Date.UTC(2026, 9, 5, 12);

const fresh = (storage) => load(FILES, { localStorage: storage || makeStorage() });
const plain = (v) => JSON.parse(JSON.stringify(v)); // vm-realm objects → this realm

// Seeds a rhythm's stats by observing answers at `t`.
function answer(A, rhythm, correct, times = 1, extra = {}, t = NOW) {
  for (let i = 0; i < times; i++) A.observe({ rhythm, category: "rhythm", correct, ...extra }, t);
}

// ---------- SM-2 scheduler ----------

test("quality: misses are 1, correct answers graded by speed", () => {
  const { EkgAdaptive: A } = fresh();
  assert.equal(A.quality(false, 2000), 1);
  assert.equal(A.quality(true, 4000), 5);
  assert.equal(A.quality(true, 15000), 4);
  assert.equal(A.quality(true, 45000), 3);
  assert.equal(A.quality(true, undefined), 4, "no timing → plain 'correct'");
});

test("schedule: 1 day, 3 days, then interval × ease", () => {
  const { EkgAdaptive: A } = fresh();
  let c = A.schedule(null, 5, NOW);
  assert.equal(c.reps, 1);
  assert.equal(c.ivl, 1);
  assert.equal(c.due, NOW + DAY);
  assert.ok(Math.abs(c.ef - 2.6) < 1e-9, "a perfect answer raises ease by 0.1");
  c = A.schedule(c, 5, NOW + DAY);
  assert.equal(c.ivl, 3);
  c = A.schedule(c, 4, NOW + 4 * DAY);
  assert.equal(c.ivl, Math.round(3 * c.ef));
  assert.equal(c.due, NOW + 4 * DAY + c.ivl * DAY);
  assert.equal(c.n, 3);
});

test("schedule: a miss resets repetitions, counts a lapse and returns in 10 minutes", () => {
  const { EkgAdaptive: A } = fresh();
  let c = A.schedule(A.schedule(null, 5, NOW), 5, NOW + DAY);
  c = A.schedule(c, 1, NOW + 2 * DAY);
  assert.equal(c.reps, 0);
  assert.equal(c.lapses, 1);
  assert.equal(c.due, NOW + 2 * DAY + 10 * MIN);
  // a miss on a never-learned card is not a lapse
  assert.equal(A.schedule(null, 1, NOW).lapses, 0);
});

test("schedule: ease never drops below 1.3", () => {
  const { EkgAdaptive: A } = fresh();
  let c = null;
  for (let i = 0; i < 20; i++) c = A.schedule(c, 1, NOW + i * MIN);
  assert.equal(c.ef, 1.3);
});

// ---------- mastery math ----------

test("mastery: zero when unseen, grows with consistent evidence", () => {
  const { EkgAdaptive: A } = fresh();
  assert.equal(A.mastery(undefined, NOW), 0);
  assert.equal(A.level(undefined, NOW), "new");
  let s = null;
  const seen = [];
  for (let i = 0; i < 8; i++) {
    s = A.updateStat(s, true, 3000, NOW);
    seen.push(A.mastery(s, NOW));
  }
  for (let i = 1; i < seen.length; i++) assert.ok(seen[i] > seen[i - 1], "each correct answer adds mastery");
  assert.ok(Math.abs(seen[5] - 0.75) < 1e-9, "6 fast, fresh, perfect answers = 75%");
  assert.equal(A.level(s, NOW), "mastered");
  assert.equal(A.tier(s, NOW), 3);
});

test("mastery: recent answers outweigh old ones", () => {
  const { EkgAdaptive: A } = fresh();
  let improving = null;
  let slipping = null;
  for (let i = 0; i < 6; i++) improving = A.updateStat(improving, i >= 3, 0, NOW);
  for (let i = 0; i < 6; i++) slipping = A.updateStat(slipping, i < 3, 0, NOW);
  assert.equal(improving.c, slipping.c);
  assert.ok(A.mastery(improving, NOW) > A.mastery(slipping, NOW));
});

test("mastery: fades without practice, but never below half", () => {
  const { EkgAdaptive: A } = fresh();
  let s = null;
  for (let i = 0; i < 6; i++) s = A.updateStat(s, true, 0, NOW);
  const m0 = A.mastery(s, NOW);
  const m30 = A.mastery(s, NOW + 30 * DAY);
  const m1000 = A.mastery(s, NOW + 1000 * DAY);
  assert.ok(m30 < m0);
  assert.ok(m1000 >= m0 / 2 - 1e-9);
  assert.equal(A.recency(0, 6), 1);
});

test("mastery: slow answers count for less", () => {
  const { EkgAdaptive: A } = fresh();
  assert.equal(A.speed(8000, 3), 1);
  assert.equal(A.speed(60000, 3), 0.85);
  assert.equal(A.speed(60000, 0), 1, "untimed history is not penalised");
  let fast = null;
  let slow = null;
  for (let i = 0; i < 5; i++) {
    fast = A.updateStat(fast, true, 4000, NOW);
    slow = A.updateStat(slow, true, 35000, NOW);
  }
  assert.ok(A.mastery(fast, NOW) > A.mastery(slow, NOW));
});

// ---------- storage, schema and migration ----------

test("migration: the v1 answer log is replayed into the v2 model and kept", () => {
  const legacy = {
    events: [
      { kind: "quiz", category: "rhythm", rhythm: "afib", focus: null, correct: true, t: NOW - 3 * DAY },
      { kind: "quiz", category: "rhythm", rhythm: "afib", focus: null, correct: false, t: NOW - 2 * DAY },
      { kind: "scenario", category: null, rhythm: "vt", focus: "tachycardia", correct: true, t: NOW - DAY },
      { garbage: true },
    ],
  };
  const storage = makeStorage({ "ekg-zoll-trainer-stats-v1": JSON.stringify(legacy) });
  const { EkgAdaptive: A } = fresh(storage);
  const s = plain(A.load(NOW));
  assert.equal(s.version, 2);
  assert.equal(s.migratedFrom, "ekg-zoll-trainer-stats-v1");
  assert.equal(s.importedEvents, 3);
  assert.equal(s.rhythms.afib.n, 2);
  assert.equal(s.rhythms.afib.c, 1);
  assert.equal(s.rhythms.afib.last, NOW - 2 * DAY);
  assert.equal(s.topics.rhythm.n, 2);
  assert.equal(s.focus.tachycardia.n, 1);
  assert.equal(Object.values(s.daily).reduce((a, b) => a + b, 0), 3);
  assert.ok(storage.getItem("ekg-mastery-v2"), "the migration is persisted");
  assert.equal(storage.getItem("ekg-zoll-trainer-stats-v1"), JSON.stringify(legacy), "the v1 log is untouched");

  // loading again does not replay (and double-count) the log
  assert.equal(plain(A.load(NOW)).rhythms.afib.n, 2);
  // a page reload reads the persisted v2 store
  assert.equal(plain(fresh(storage).EkgAdaptive.load(NOW)).rhythms.afib.n, 2);
});

test("migration: missing or corrupt data gives an empty current-schema store", () => {
  const { EkgAdaptive: A } = fresh();
  const empty = plain(A.migrate(null, null, NOW));
  assert.equal(empty.version, 2);
  assert.deepEqual(empty.rhythms, {});
  const storage = makeStorage({ "ekg-mastery-v2": "{nope", "ekg-zoll-trainer-stats-v1": "[1,2" });
  assert.equal(plain(fresh(storage).EkgAdaptive.load(NOW)).version, 2);
  // a v2 store with a broken section keeps the good sections
  const partial = plain(A.migrate({ version: 2, rhythms: { nsr: { n: 1, c: 1, ema: 1, ms: 0, msN: 0, last: NOW } }, items: "x" }, null, NOW));
  assert.equal(partial.rhythms.nsr.n, 1);
  assert.deepEqual(partial.items, {});
});

test("a store written by a newer schema is read but never overwritten", () => {
  const future = JSON.stringify({ version: 3, rhythms: { nsr: { n: 4, c: 4, ema: 1, ms: 0, msN: 0, last: NOW } }, extra: 1 });
  const storage = makeStorage({ "ekg-mastery-v2": future });
  const { EkgAdaptive: A } = fresh(storage);
  assert.equal(plain(A.load(NOW)).rhythms.nsr.n, 4);
  A.observe({ rhythm: "nsr", correct: true }, NOW);
  assert.equal(storage.getItem("ekg-mastery-v2"), future);
});

test("every EkgStats answer flows into the model: stats, review card, confusion", () => {
  const ctx = fresh();
  const A = ctx.EkgAdaptive;
  ctx.EkgStats.record({ kind: "sprint", category: "rhythm", rhythm: "vt", focus: null, correct: false, item: "s:vt", ms: 3000, chosen: "torsades" });
  ctx.EkgStats.record({ kind: "quiz", category: "meds", rhythm: "svt", focus: null, correct: true, item: "q:5", ms: 9000 });
  ctx.EkgStats.record({ kind: "scenario", category: null, rhythm: "vf_coarse", focus: "vf-pulseless", correct: true });
  const s = plain(A.load());
  assert.equal(s.rhythms.vt.n, 1);
  assert.equal(s.rhythms.vt.ms, 3000);
  assert.equal(s.items["s:vt"].reps, 0);
  assert.equal(s.items["q:5"].reps, 1);
  assert.equal(s.confusions["vt>torsades"], 1);
  assert.equal(s.topics.meds.n, 1);
  assert.equal(s.focus["vf-pulseless"].n, 1);
  assert.equal(ctx.EkgStats.profile().total, 3, "the v1 log still records too");
});

test("storage that throws never breaks recording or planning", () => {
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
  const ctx = fresh(throwing);
  assert.doesNotThrow(() => ctx.EkgStats.record({ category: "meds", rhythm: "nsr", correct: true }));
  assert.equal(ctx.EkgAdaptive.planSession({ now: NOW }).length, 15);
  assert.doesNotThrow(() => ctx.EkgAdaptive.reset());
});

// ---------- planning ----------

test("planSession: due reviews come first, capped at 60% of the round", () => {
  const { EkgAdaptive: A } = fresh();
  const rhythms = ["nsr", "afib", "vt", "svt", "avb3", "junctional", "asystole", "wpw", "stemi", "hyperk", "aivr", "torsades"];
  rhythms.forEach((r, i) => A.observe({ rhythm: r, category: "rhythm", correct: true, item: "s:" + r, ms: 3000 }, NOW - 5 * DAY - i));
  const plan = A.planSession({ n: 10, now: NOW, rand: () => 0.5 });
  assert.equal(plan.length, 10);
  const due = plan.filter((p) => p.reason === "due");
  assert.equal(due.length, 6);
  assert.ok(due.every((p) => p.kind === "strip" && rhythms.includes(p.rhythm)));
});

test("planSession: weak rhythms and their look-alikes outrank mastered ones", () => {
  const { EkgAdaptive: A } = fresh();
  answer(A, "nsr", true, 10);
  answer(A, "afib", false, 4, {}, NOW - 2 * DAY);
  const plan = A.planSession({ n: 12, now: NOW, rand: () => 0.5 });
  const count = (r) => plan.filter((p) => p.rhythm === r).length;
  assert.equal(plan[0].rhythm, "afib", "the proven weak spot leads the round");
  assert.ok(count("afib") >= 2);
  assert.equal(count("nsr"), 0, "a mastered, not-due rhythm waits");
  const alikes = new Set(A.lookAlikes("afib"));
  assert.ok(plan.some((p) => alikes.has(p.rhythm) && p.reason === "look-alike"), "afib's look-alikes are drilled alongside it");
  assert.equal(plan.find((p) => p.rhythm === "afib").tier, 1, "a weak rhythm starts at tier 1");
});

test("planSession: variety rules — ≤3 per rhythm, never the same rhythm twice in a row", () => {
  const { EkgAdaptive: A } = fresh();
  answer(A, "vt", false, 5);
  answer(A, "torsades", false, 5);
  for (let seed = 0; seed < 5; seed++) {
    let x = seed + 1;
    const rand = () => ((x = (x * 16807) % 2147483647) / 2147483647);
    const plan = A.planSession({ n: 15, now: NOW, rand });
    assert.equal(plan.length, 15);
    assert.equal(new Set(plan.map((p) => p.id)).size, 15, "no item twice");
    const per = {};
    plan.forEach((p) => (per[p.rhythm] = (per[p.rhythm] || 0) + 1));
    assert.ok(Object.values(per).every((n) => n <= 3));
    for (let i = 1; i < plan.length; i++) assert.notEqual(plan[i].rhythm, plan[i - 1].rhythm);
  }
});

test("planSession: difficulty follows mastery — strips for beginners, management once mastered", () => {
  const beginner = fresh().EkgAdaptive.planSession({ n: 15, now: NOW, rand: () => 0.5 });
  assert.ok(beginner.every((p) => p.tier === 1));
  assert.ok(beginner.every((p) => p.kind === "strip" || p.topic === "rhythm"), "a new learner starts with recognition");

  const { EkgAdaptive: A } = fresh();
  for (const r of A.rhythmMastery(NOW)) answer(A, r.key, true, 10);
  const expert = A.planSession({ n: 15, now: NOW, rand: () => 0.5 });
  assert.ok(expert.every((p) => p.tier === 3));
  assert.ok(expert.every((p) => p.kind === "question" && p.topic !== "rhythm"), "mastered rhythms move on to meds, condition and code");
});

test("stripChoices: tier 1 avoids look-alikes; tier 3 leads with personal confusions", () => {
  const { EkgAdaptive: A } = fresh();
  const alikes = new Set(A.lookAlikes("afib"));
  for (let i = 0; i < 10; i++) {
    const c = [...A.stripChoices("afib", 1, { now: NOW })];
    assert.equal(c.length, 4);
    assert.equal(new Set(c).size, 4);
    assert.ok(c.includes("afib"));
    assert.ok(c.every((k) => k === "afib" || !alikes.has(k)));
  }
  A.observe({ rhythm: "afib", category: "rhythm", correct: false, chosen: "pvc_bigeminy" }, NOW);
  for (let i = 0; i < 10; i++) {
    const c = [...A.stripChoices("afib", 3, { now: NOW })];
    assert.ok(c.includes("pvc_bigeminy"), "the rhythm this learner picked by mistake is offered again");
    assert.ok(c.filter((k) => alikes.has(k)).length >= 2);
  }
});

test("dailyPlan: due count, weak rhythms, goal, streak and a study suggestion", () => {
  const { EkgAdaptive: A } = fresh();
  let p = A.dailyPlan(NOW);
  assert.equal(p.hasHistory, false);
  assert.equal(p.doneToday, 0);
  assert.equal(p.steps[0].kind, "new");
  assert.equal(p.total, p.rhythms.length);

  A.observe({ rhythm: "mobitz2", category: "rhythm", correct: false, item: "s:mobitz2" }, NOW - 2 * DAY);
  A.observe({ rhythm: "mobitz2", category: "rhythm", correct: false, item: "s:mobitz2" }, NOW - DAY);
  A.observe({ rhythm: "nsr", category: "rhythm", correct: true }, NOW);
  p = A.dailyPlan(NOW);
  assert.equal(p.dueCount, 1);
  assert.equal(p.steps[0].kind, "review");
  assert.equal(p.weak[0].key, "mobitz2");
  assert.equal(p.studyKey, "mobitz2");
  assert.equal(p.doneToday, 1);
  assert.equal(p.streak, 3);
  assert.equal(A.dailyPlan(NOW + 2 * DAY).streak, 0, "a missed day ends the streak");
});
