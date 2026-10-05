const test = require("node:test");
const assert = require("node:assert/strict");
const vm = require("vm");
const { load, makeStorage } = require("./load");
const { validateScenario } = require("./validate");

function env() {
  const ctx = load(["ekg-rhythms.js", "ekg-scenarios.js", "ekg-scenario-engine.js"]);
  return {
    E: ctx.EkgScenarioEngine,
    scenarios: vm.runInContext("EKG_SCENARIOS", ctx),
    rhythms: Object.keys(vm.runInContext("RHYTHMS", ctx)),
  };
}

// Deterministic rng for choice shaping.
function rngFrom(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a * 1664525 + 1013904223) >>> 0;
    return a / 4294967296;
  };
}

const byId = (scenarios, id) => scenarios.find((s) => s.id === id);
const right = (run, extra = {}) => ({ correct: true, decisionMs: 3000, atMs: 0, ...extra });
const wrong = (run, extra = {}) => ({ correct: false, decisionMs: 3000, atMs: 0, ...extra });

// Every choice and keyed answer an engine stage shows must already exist in
// the app's vetted content — the engine may never invent clinical text.
function allChoices(scenarios) {
  const set = new Set();
  for (const s of scenarios) for (const st of s.stages) st.choices.forEach((c) => set.add(c));
  return set;
}

test("keyed answers walk every authored scenario straight through, no branches", () => {
  const { E, scenarios } = env();
  for (const s of scenarios) {
    const run = E.createRun(s, "standard");
    let steps = 0;
    for (;;) {
      const res = E.resolve(run, right(run));
      assert.equal(res.next, "advance", `${s.id}: ${res.next}`);
      assert.equal(res.reveal, true);
      steps++;
      if (E.advance(run) === "debrief") break;
    }
    assert.equal(steps, s.stages.length, s.id);
    assert.equal(run.correct, s.stages.length);
    assert.equal(run.timeline.length, s.stages.length);
    assert.equal(E.metrics(run).branches, 0);
  }
});

test("missed shock: VF persists and fades, the same vetted decision is re-posed", () => {
  const { E, scenarios, rhythms } = env();
  const s = byId(scenarios, "vf-arrest");
  const run = E.createRun(s, "standard");
  E.resolve(run, right(run));
  E.advance(run);
  const shock = run.current;
  assert.ok(E.isShockStage(shock));
  const res = E.resolve(run, wrong(run));
  assert.equal(res.next, "branch");
  assert.equal(res.reveal, false, "a re-posed decision must not reveal its answer first");
  assert.equal(E.advance(run), "stage");
  const b = run.current;
  assert.equal(run.stageIdx, 1);
  assert.equal(run.attempt, 1);
  assert.equal(b.rhythm, "vf_fine");
  assert.ok(b.vitals.etco2 < shock.vitals.etco2, "ETCO2 falls");
  assert.equal(b.question, shock.question);
  assert.deepEqual([...b.choices], [...shock.choices]);
  assert.equal(b.answer, shock.answer);
  assert.equal(b.rationale, shock.rationale);
  assert.match(b.narrative, /fading toward fine VF/);
  validateScenario({ id: "branch", title: "t", blurb: "b", stages: [b] }, rhythms);

  // getting it right on the branch recovers, but the score counts first tries only
  const r2 = E.resolve(run, right(run));
  assert.equal(r2.next, "advance");
  assert.equal(run.correct, 1);
  assert.equal(run.recovered, 1);
  E.advance(run);
  assert.equal(run.stageIdx, 2);
  assert.equal(run.current.rhythm, "vf_coarse", "the next authored stage is shown as written");
});

test("two critical misses lose the patient on standard; guided gives a third chance", () => {
  const { E, scenarios } = env();
  const s = byId(scenarios, "vf-arrest");
  const std = E.createRun(s, "standard");
  E.resolve(std, wrong(std));
  E.advance(std);
  const res = E.resolve(std, wrong(std));
  assert.equal(res.next, "death");
  assert.equal(std.death, true);
  assert.equal(E.advance(std), "death");

  const g = E.createRun(s, "guided");
  E.resolve(g, wrong(g));
  E.advance(g);
  const r2 = E.resolve(g, wrong(g));
  assert.equal(r2.next, "takeover", "out of re-poses: the team leader directs the action");
  assert.equal(r2.reveal, true);
  assert.match(E.outcomeText(g, r2), /^The team leader steps in and directs it: CPR started/);
  assert.equal(E.advance(g), "stage");
  assert.equal(g.stageIdx, 1);
  assert.equal(g.death, false);
});

test("wrong treatment with a pulse: the patient deteriorates before the re-pose", () => {
  const { E, scenarios } = env();
  const s = byId(scenarios, "unstable-brady");
  const run = E.createRun(s, "standard");
  const before = run.current;
  assert.equal(before.scene.pulse, "PRESENT");
  E.resolve(run, wrong(run));
  E.advance(run);
  const b = run.current;
  const [sb, db] = before.vitals.nibp.split("/").map(Number);
  const [sa, da] = b.vitals.nibp.split("/").map(Number);
  assert.ok(sa < sb && da < db, `${before.vitals.nibp} → ${b.vitals.nibp}`);
  assert.ok(b.vitals.spo2 < before.vitals.spo2);
  assert.equal(b.scene.pulse, "WEAK");
  assert.equal(b.scene.loc, "ALTERED");
  assert.ok(E.isCritical(b), "a weakened pulse makes the re-pose time-critical");
  assert.match(b.narrative, /getting worse — BP \d+\/\d+, SpO2 \d+%/);
  assert.equal(b.choices[b.answer], before.choices[before.answer]);
});

test("a delayed shock lets VF persist on standard, but not on guided", () => {
  const { E, scenarios } = env();
  const s = byId(scenarios, "vf-arrest");
  const run = E.createRun(s, "standard");
  E.resolve(run, right(run));
  E.advance(run);
  const res = E.resolve(run, right(run, { decisionMs: 31000 }));
  assert.equal(res.late, true);
  assert.equal(res.next, "branch");
  assert.equal(res.reason, "late-shock");
  assert.equal(res.reveal, true, "the call was right — show why");
  assert.equal(run.correct, 2, "a late but correct decision still scores");
  E.advance(run);
  assert.equal(run.current.rhythm, "vf_fine");
  assert.match(run.current.narrative, /The shock came late/);

  const g = E.createRun(s, "guided");
  E.resolve(g, right(g));
  E.advance(g);
  const rg = E.resolve(g, right(g, { decisionMs: 120000 }));
  assert.equal(rg.late, false);
  assert.equal(rg.next, "advance");
});

test("late decisions on non-shock stages are flagged but don't branch", () => {
  const { E, scenarios } = env();
  const run = E.createRun(byId(scenarios, "vf-arrest"), "standard");
  const res = E.resolve(run, right(run, { decisionMs: 45000 }));
  assert.equal(res.late, true);
  assert.equal(res.next, "advance");
  assert.match(E.outcomeText(run, res), /took 45 s with no pulse/);
});

test("expert: decision clock, timeouts branch, no rhythm name on the alarm", () => {
  const { E, scenarios } = env();
  const run = E.createRun(byId(scenarios, "unstable-brady"), "expert", { rng: rngFrom(3) });
  assert.equal(run.cfg.alarmNamesRhythm, false);
  assert.equal(E.timeLimitMs(run), run.cfg.limitMs, "pulse present: the longer clock");
  const res = E.resolve(run, { correct: true, timedOut: true, decisionMs: 45000, atMs: 45000 });
  assert.equal(res.correct, false, "a timeout is never a correct decision");
  assert.equal(res.next, "branch");
  assert.equal(run.misses[0].timedOut, true);
  E.advance(run);
  assert.match(run.current.narrative, /^No decision was made in time\./);
  assert.equal(E.timeLimitMs(run), run.cfg.limitCriticalMs, "weak pulse: the short clock");
  assert.equal(E.metrics(run).timeouts, 1);
});

test("guided removes a wrong option; expert adds a vetted distractor — answers never change", () => {
  const { E, scenarios } = env();
  const vetted = allChoices(scenarios);
  const keyed = new Set(scenarios.flatMap((s) => s.stages.map((st) => st.choices[st.answer].toLowerCase())));
  const pool = E.buildDistractorPool(scenarios);
  let added = 0;
  for (const s of scenarios) {
    for (const [diff, seed] of [["guided", 1], ["expert", 2]]) {
      const run = E.createRun(s, diff, { rng: rngFrom(seed), pool });
      for (let i = 0; i < s.stages.length; i++) {
        const base = s.stages[run.stageIdx];
        const st = run.current;
        assert.equal(st.choices[st.answer], base.choices[base.answer], `${s.id} ${diff} stage ${i + 1}: keyed answer`);
        assert.equal(new Set(st.choices).size, st.choices.length, "no duplicate choices");
        for (const c of st.choices) assert.ok(vetted.has(c), `invented choice: ${c}`);
        if (diff === "guided") assert.equal(st.choices.length, base.choices.length - 1);
        if (diff === "expert") {
          const extra = st.choices.slice(base.choices.length);
          added += extra.length;
          for (const c of extra) assert.ok(!keyed.has(c.toLowerCase()), `distractor is a keyed answer somewhere: ${c}`);
        }
        E.resolve(run, right(run));
        E.advance(run);
      }
    }
  }
  assert.ok(added > 0, "expert should find at least some same-question distractors");
});

test("distractors only come from stages with the same question and keyed answer", () => {
  const { E } = env();
  const mk = (q, choices) => ({ question: q, choices, answer: 0 });
  const pool = E.buildDistractorPool([
    { stages: [mk("Q1?", ["Right", "Wrong A", "Wrong B"]), mk("Q1?", ["Right", "Wrong C", "Wrong A"])] },
    { stages: [mk("Q2?", ["Wrong C", "Other"]), mk("Q1?", ["Different keyed", "Wrong D"])] },
  ]);
  const extra = E.distractorsFor(mk("Q1?", ["Right", "Wrong A"]), pool, 5, rngFrom(1));
  // Wrong C is keyed correct for Q2 → excluded; Wrong D belongs to a different keyed answer
  assert.deepEqual([...extra].sort(), ["Wrong B"]);

  // second tier: same keyed move on the same rhythm and pulse state
  const shock = (q, choices) => ({ ...mk(q, choices), rhythm: "vf_coarse", scene: { pulse: "ABSENT" }, intervention: "Defibrillated 200 J" });
  const pool2 = E.buildDistractorPool([
    { stages: [shock("Now what?", ["Shock", "Stack three shocks"]), { ...shock("Now?", ["Shock", "Wait"]), rhythm: "vt", scene: { pulse: "PRESENT" } }] },
  ]);
  assert.deepEqual([...E.distractorsFor(shock("Next step?", ["Shock now", "Epi first"]), pool2, 5, rngFrom(1))], ["Stack three shocks"]);
});

test("branch narratives never carry doses or energies", () => {
  const { E, scenarios } = env();
  for (const s of scenarios) {
    for (const st of s.stages) {
      for (const reason of ["wrong", "timeout", "late-shock"]) {
        const b = E.degrade(st, 2, reason);
        assert.doesNotMatch(b.narrative, /\d\s*(mg|mcg|J|joules|units|g)\b/i, `${s.id}: ${b.narrative}`);
        assert.equal(b.intervention, st.intervention);
        assert.equal(b.rationale, st.rationale);
      }
    }
  }
});

test("debrief: ideal-sequence comparison and ACLS timing metrics", () => {
  const { E, scenarios } = env();
  const s = byId(scenarios, "vf-arrest");
  const run = E.createRun(s, "standard");
  // CPR at 0:08, shock missed at 0:20 then given at 0:41, epi late at 1:30, amio, then exit
  E.resolve(run, right(run, { atMs: 8000 }));
  E.advance(run);
  E.resolve(run, wrong(run, { atMs: 20000 }));
  E.advance(run);
  E.resolve(run, right(run, { atMs: 41000 }));
  E.advance(run);
  E.resolve(run, right(run, { atMs: 90000, decisionMs: 40000 }));
  E.advance(run);
  const m = E.metrics(run);
  assert.equal(m.timeToFirstShockMs, 41000);
  assert.equal(m.timeToEpiMs, 90000);
  assert.equal(m.timeToCardioversionMs, null);
  assert.equal(m.branches, 1);
  assert.equal(m.lateDecisions, 1);
  assert.equal(m.recovered, 1);
  const rows = E.compare(run);
  assert.deepEqual(
    [...rows.map((r) => r.status)],
    ["on-time", "recovered", "late", "not-reached", "not-reached"]
  );
  assert.equal(rows[1].t, 41000);
  assert.equal(rows[1].attempts, 2);
  assert.equal(rows[0].text, s.stages[0].intervention);
});

test("intervention classifier", () => {
  const { E } = env();
  const k = (t) => E.classify(t);
  assert.equal(k("Defibrillated 200 J (1st shock)").shock, true);
  assert.equal(k("Synchronized cardioversion 75 J").shock, false);
  assert.equal(k("Synchronized cardioversion 75 J").cardioversion, true);
  assert.equal(k("Prepared synchronized cardioversion").cardioversion, false);
  assert.equal(k("Epinephrine 1 mg IV").epi, true);
  assert.equal(k("Epi 1 mg · calcium repeated").epi, true);
  assert.equal(k("Epinephrine infusion started").epi, false);
  assert.equal(k("Amiodarone 300 mg IV · 4th shock").antiarrhythmic, true);
});

// ---------- code log + generator targeting ----------

function genEnv(storage = makeStorage(), seed = 7) {
  const ctx = load(["ekg-rhythms.js", "ekg-education.js", "ekg-stats.js", "ekg-codelog.js", "ekg-generator.js"], {
    localStorage: storage,
    seed,
  });
  return { G: ctx.EkgGenerator, L: ctx.EkgCodeLog };
}

const tl = (rhythm, correct, n, attempt = 0) => Array.from({ length: n }, () => ({ rhythm, correct, attempt, t: 0 }));

test("code log: per-rhythm weakness counts first attempts, with a legacy fallback", () => {
  const { L } = genEnv();
  L.add({ outcome: "completed", misses: [], timeline: [...tl("svt", false, 3), ...tl("svt", true, 1), ...tl("svt", true, 2, 1)] });
  L.add({ outcome: "died", misses: [{ rhythm: "vf_coarse" }, { question: "old entry without rhythm" }] });
  const w = L.rhythmWeakness();
  assert.deepEqual({ ...w.svt }, { seen: 4, missed: 3, rate: 0.75 });
  assert.deepEqual({ ...w.vf_coarse }, { seen: 1, missed: 1, rate: 1 });
  assert.equal(Object.keys(w).length, 2);
});

test("code log keeps difficulty, metrics and timeline on entries", () => {
  const { L } = genEnv();
  L.add({ outcome: "completed", difficulty: "expert", metrics: { timeToFirstShockMs: 41000 }, timeline: tl("vf_coarse", true, 1), misses: [] });
  const [e] = L.list();
  assert.equal(e.difficulty, "expert");
  assert.equal(e.metrics.timeToFirstShockMs, 41000);
  assert.equal(e.timeline.length, 1);
  assert.equal(L.focusHint("bradycardia").startsWith("review the bradycardia ladder"), true);
  assert.equal(L.focusHint("nope"), null);
});

test("generator targets the weakest rhythm in the code log", () => {
  const { G, L } = genEnv();
  for (let i = 0; i < 4; i++) {
    L.add({ outcome: "completed", misses: [], timeline: [...tl("svt", false, 2), ...tl("vf_coarse", true, 2), ...tl("asystole", true, 2), ...tl("sinus_brady", true, 2), ...tl("torsades", true, 2)] });
  }
  const scores = G.arcScores();
  const tachy = scores.find((s) => s.key === "tachy");
  assert.equal(tachy.weak.rhythm, "svt");
  assert.ok(scores.every((s) => s.key === "tachy" || s.score < tachy.score), JSON.stringify(scores));

  const counts = {};
  for (let i = 0; i < 300; i++) {
    const s = G.generate();
    counts[s.arc] = (counts[s.arc] || 0) + 1;
  }
  const maxOther = Math.max(...Object.entries(counts).filter(([k]) => k !== "tachy").map(([, v]) => v));
  assert.ok(counts.tachy > maxOther, JSON.stringify(counts));
  const t = G.listGenerated().find((s) => s.arc === "tachy");
  assert.equal(t.targetRhythm, "svt");
  assert.match(t.reason, /^Targeting a weak rhythm from your code log: you missed 8 of 8 decisions on supraventricular tachycardia/i);
});

test("generator without code-log history behaves as before", () => {
  const { G } = genEnv();
  const s = G.generate();
  assert.equal(s.targetRhythm, null);
  assert.match(s.reason, /Exploring the case library/);
});
