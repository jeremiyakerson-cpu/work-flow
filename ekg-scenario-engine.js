/*
 * Adaptive scenario engine: the run-state logic behind the code simulator —
 * difficulty levels, branching (a wrong, late, or timed-out decision changes
 * what the patient does next), the action timeline, and ACLS timing
 * metrics. No DOM here: ekg-scenario.js renders whatever stage the run
 * hands it, which keeps every transition unit-testable.
 *
 * Branches never invent clinical content. A branch re-poses an existing,
 * vetted stage (same question, choices, keyed answer and rationale) with
 * the patient's rhythm/vitals degraded and an engine-written narrative
 * describing the consequence. Doses, energies and answers are untouched.
 * Educational use only.
 */

(function () {
  // ---------- difficulty levels ----------

  const DIFFICULTY = {
    guided: {
      key: "guided",
      label: "Guided",
      blurb: "Hints on every decision, one wrong option removed, no clock pressure.",
      hints: true,
      eliminate: 1, // wrong choices removed per decision
      alarmNamesRhythm: true,
      lateMs: null, // no "too slow" consequences
      limitMs: null, // no hard decision clock
      limitCriticalMs: null,
      distractors: 0,
      interruptions: 0,
      criticalLimit: 3, // critical misses before the patient is lost
      maxRetries: 1, // branch re-poses per decision before the team leader takes over
    },
    standard: {
      key: "standard",
      label: "Standard",
      blurb: "No hints. Slow decisions with no pulse have consequences — a delayed shock lets VF persist.",
      hints: false,
      eliminate: 0,
      alarmNamesRhythm: true,
      lateMs: 30000,
      limitMs: null,
      limitCriticalMs: null,
      distractors: 0,
      interruptions: 0,
      criticalLimit: 2,
      maxRetries: 1,
    },
    expert: {
      key: "expert",
      label: "Expert",
      blurb: "No hints, the alarm doesn't name the rhythm, a decision clock, an extra distractor option, and interruptions.",
      hints: false,
      eliminate: 0,
      alarmNamesRhythm: false,
      lateMs: 15000,
      limitMs: 45000,
      limitCriticalMs: 20000,
      distractors: 1,
      interruptions: 0.35, // chance per decision (never the first) of a non-clinical interruption
      criticalLimit: 2,
      maxRetries: 1,
    },
  };
  const DEFAULT_DIFFICULTY = "standard";

  function difficultyConfig(key) {
    return DIFFICULTY[key] || DIFFICULTY[DEFAULT_DIFFICULTY];
  }

  // Non-clinical interruptions for expert mode: they cost clock time,
  // nothing else.
  const INTERRUPTIONS = [
    "A family member appears in the doorway asking what's happening.",
    "The unit phone rings — another patient's family wants an update.",
    "A colleague asks you to co-sign a medication for a patient down the hall.",
    "The overhead page calls a rapid response to another room.",
    "A student nurse asks where the extra suction canister is kept.",
  ];

  // ---------- stage helpers ----------

  const SHOCKABLE = ["vf_coarse", "vf_fine", "vt", "torsades"];
  const LOC_DOWN = { ALERT: "ALTERED", ALTERED: "ALTERED", UNRESPONSIVE: "UNRESPONSIVE" };

  function isCritical(st) {
    return !!(st && st.scene && st.scene.pulse !== "PRESENT");
  }

  // What an intervention line says was done. Drives the timeline metrics
  // and "delayed shock" branching; read off the existing vetted text.
  function classify(text) {
    const t = text || "";
    const cardioversion = /cardiover/i.test(t) && !/prepar/i.test(t);
    return {
      shock: /defibrillat|\bshock\b/i.test(t) && !/cardiover/i.test(t),
      cardioversion,
      epi: /\bepi(nephrine)?\b/i.test(t) && !/infusion|drip/i.test(t),
      antiarrhythmic: /amiodarone|lidocaine|procainamide/i.test(t),
      cpr: /\bCPR\b/.test(t),
    };
  }

  // A stage whose keyed action is defibrillating a pulseless shockable rhythm.
  function isShockStage(st) {
    if (!st || !st.scene || st.scene.pulse !== "ABSENT" || !SHOCKABLE.includes(st.rhythm)) return false;
    const k = classify(st.intervention);
    return k.shock && !k.antiarrhythmic && !k.epi;
  }

  function cloneStage(st) {
    return { ...st, vitals: { ...(st.vitals || {}) }, scene: { ...(st.scene || {}) }, choices: st.choices.slice() };
  }

  const norm = (s) => String(s || "").trim().toLowerCase().replace(/\s+/g, " ");

  // The patient's state after a wrong, late, or missing decision. `level`
  // grows with each consequence on the same decision.
  function degrade(stage, level, reason) {
    const st = cloneStage(stage);
    const v = st.vitals;
    const prefix = reason === "timeout" ? "No decision was made in time. " : "";
    if (st.scene.pulse === "ABSENT") {
      const fading = st.rhythm === "vf_coarse" || st.rhythm === "vf_fine";
      if (st.rhythm === "vf_coarse") st.rhythm = "vf_fine";
      if (v.etco2 != null) v.etco2 = Math.max(6, v.etco2 - 3 * level);
      if (reason === "late-shock") {
        st.narrative = fading
          ? "The shock came late. VF persisted through the delay and is fading toward fine VF on the monitor. Compressions continue and the defibrillator is charged again."
          : "The shock came late. The rhythm persisted through the delay — still no pulse. Compressions continue and the defibrillator is charged again.";
      } else if (SHOCKABLE.includes(st.rhythm)) {
        st.narrative =
          prefix +
          `That didn't treat the rhythm. ${fading ? "The VF is fading toward fine VF" : "The rhythm persists"}, there is still no pulse, and the ETCO2 is drifting down. The team looks to you again.`;
      } else {
        st.narrative =
          prefix + "Still no pulse. That didn't move the code forward, and the ETCO2 is drifting down. The team looks to you again.";
      }
    } else {
      const m = /^(\d+)\/(\d+)$/.exec(v.nibp || "");
      if (m) v.nibp = `${Math.max(50, Number(m[1]) - 8 * level)}/${Math.max(30, Number(m[2]) - 5 * level)}`;
      if (v.spo2 != null) v.spo2 = Math.max(80, v.spo2 - 3 * level);
      st.scene.loc = LOC_DOWN[st.scene.loc] || st.scene.loc;
      if (st.scene.pulse === "PRESENT") st.scene.pulse = "WEAK";
      const bits = [];
      if (m) bits.push(`BP ${v.nibp}`);
      if (v.spo2 != null) bits.push(`SpO2 ${v.spo2}%`);
      st.narrative =
        prefix +
        `The patient is getting worse${bits.length ? ` — ${bits.join(", ")}` : ""}, the pulse is weaker, and they are ${st.scene.loc.toLowerCase()}. That didn't fix the problem. The team looks to you again.`;
    }
    st.branch = reason;
    return st;
  }

  // ---------- choice shaping (guided elimination, expert distractors) ----------

  // Remove `n` wrong choices; the keyed answer is remapped, never changed.
  function eliminate(stage, n, rng = Math.random) {
    if (!n) return stage;
    const st = cloneStage(stage);
    const wrong = st.choices.map((_, i) => i).filter((i) => i !== st.answer);
    const keepMin = 1; // always leave at least one wrong option
    const drop = new Set();
    while (drop.size < Math.min(n, wrong.length - keepMin)) drop.add(wrong[Math.floor(rng() * wrong.length)]);
    const answerText = st.choices[st.answer];
    st.choices = st.choices.filter((_, i) => !drop.has(i));
    st.answer = st.choices.indexOf(answerText);
    st.eliminated = drop.size;
    return st;
  }

  // Distractors are wrong choices borrowed from other stages, only where
  // they are wrong here by construction:
  //   1. stages asking the SAME question with the SAME keyed answer, then
  //   2. stages making the same kind of keyed move (shock / epi /
  //      antiarrhythmic / cardioversion) on the same rhythm and pulse state.
  // Anything keyed correct anywhere in the app is never used.
  function actionKey(st) {
    const k = classify(st.intervention);
    const kinds = ["shock", "epi", "antiarrhythmic", "cardioversion"].filter((x) => k[x]);
    if (!kinds.length || !st.scene) return null;
    return `${st.rhythm}|${st.scene.pulse}|${kinds.join("+")}`;
  }

  function buildDistractorPool(scenarios) {
    const byKey = new Map();
    const byAction = new Map();
    const allCorrect = new Set();
    const addTo = (map, key, st) => {
      if (!key) return;
      if (!map.has(key)) map.set(key, new Map());
      st.choices.forEach((c, i) => {
        if (i !== st.answer) map.get(key).set(norm(c), c);
      });
    };
    for (const s of scenarios || []) {
      for (const st of s.stages || []) {
        const correct = st.choices[st.answer];
        allCorrect.add(norm(correct));
        addTo(byKey, `${norm(st.question)}||${norm(correct)}`, st);
        addTo(byAction, actionKey(st), st);
      }
    }
    return { byKey, byAction, allCorrect };
  }

  function distractorsFor(stage, pool, n, rng = Math.random) {
    if (!n || !pool) return [];
    const have = new Set(stage.choices.map(norm));
    const tiers = [pool.byKey.get(`${norm(stage.question)}||${norm(stage.choices[stage.answer])}`), pool.byAction && pool.byAction.get(actionKey(stage))];
    const out = [];
    for (const tier of tiers) {
      if (!tier) continue;
      const candidates = [...tier].filter(([k]) => !have.has(k) && !pool.allCorrect.has(k)).map(([, text]) => text);
      while (out.length < n && candidates.length) {
        const c = candidates.splice(Math.floor(rng() * candidates.length), 1)[0];
        have.add(norm(c));
        out.push(c);
      }
    }
    return out;
  }

  function withDistractors(stage, pool, n, rng) {
    const extra = distractorsFor(stage, pool, n, rng);
    if (!extra.length) return stage;
    const st = cloneStage(stage);
    st.choices = st.choices.concat(extra); // appended: the answer index is unchanged
    st.distractors = extra.length;
    return st;
  }

  // ---------- the run ----------

  function createRun(scenario, difficulty, opts = {}) {
    const cfg = difficultyConfig(difficulty);
    const run = {
      scenario,
      difficulty: cfg.key,
      cfg,
      rng: opts.rng || Math.random,
      pool: opts.pool || null,
      stageIdx: 0,
      attempt: 0, // 0 = first time this decision is posed
      level: 0, // deterioration on the current decision
      correct: 0, // first-try correct decisions (the score)
      recovered: 0, // decisions gotten right on a branch re-pose
      misses: [],
      timeline: [],
      criticalMisses: 0,
      death: false,
      done: false,
      pending: null,
      current: null,
    };
    run.current = present(run, scenario.stages[0]);
    return run;
  }

  // Apply the difficulty's choice shaping to the stage about to be shown.
  function present(run, stage) {
    let st = stage;
    if (run.cfg.eliminate) st = eliminate(st, run.cfg.eliminate, run.rng);
    if (run.cfg.distractors) st = withDistractors(st, run.pool, run.cfg.distractors, run.rng);
    return st;
  }

  function baseStage(run) {
    return run.scenario.stages[run.stageIdx];
  }

  function timeLimitMs(run, st = run.current) {
    return isCritical(st) ? run.cfg.limitCriticalMs : run.cfg.limitMs;
  }

  function shouldInterrupt(run) {
    return !!run.cfg.interruptions && run.stageIdx > 0 && run.attempt === 0 && run.rng() < run.cfg.interruptions;
  }

  function pickInterruption(run) {
    return INTERRUPTIONS[Math.floor(run.rng() * INTERRUPTIONS.length)];
  }

  /*
   * Grade the current decision and decide where the patient goes next.
   * a: { correct, atMs (since the run started), decisionMs (time on this
   *      decision), timedOut }
   * Returns { correct, critical, late, next, reveal } where next is
   *   "branch"   — the same decision is re-posed with the patient degraded
   *   "takeover" — out of re-poses: the team leader directs the action
   *   "advance"  — on to the next authored stage (or the debrief)
   *   "death"    — critical-miss limit reached
   * and reveal says whether to show the keyed answer + rationale now (not
   * on a branch, so the re-pose is still a real decision).
   */
  function resolve(run, a) {
    const st = run.current;
    const cfg = run.cfg;
    const correct = !!a.correct && !a.timedOut;
    const critical = isCritical(st);
    const decisionMs = Math.max(0, a.decisionMs || 0);
    const late = correct && critical && cfg.lateMs != null && decisionMs > cfg.lateMs;
    const firstTry = run.attempt === 0;
    if (correct && firstTry) run.correct++;
    if (correct && !firstTry) run.recovered++;

    if (!correct) {
      run.misses.push({
        question: st.question,
        intervention: st.intervention,
        rationale: st.rationale,
        critical,
        rhythm: st.rhythm,
        timedOut: !!a.timedOut,
      });
      if (critical) {
        run.criticalMisses++;
        if (run.criticalMisses >= cfg.criticalLimit) run.death = true;
      }
    }

    let next;
    let reason = null;
    const canBranch = run.attempt < cfg.maxRetries;
    if (run.death) next = "death";
    else if (!correct && canBranch) {
      next = "branch";
      reason = a.timedOut ? "timeout" : "wrong";
    } else if (late && isShockStage(st) && canBranch) {
      next = "branch";
      reason = "late-shock";
    } else if (!correct) next = "takeover";
    else next = "advance";

    run.timeline.push({
      t: Math.max(0, a.atMs || 0),
      stage: run.stageIdx,
      attempt: run.attempt,
      text: st.intervention,
      rhythm: st.rhythm,
      correct,
      late,
      timedOut: !!a.timedOut,
      critical,
      decisionMs,
      branch: st.branch || null,
      next,
    });
    run.pending = { next, reason };
    return { correct, critical, late, next, reason, reveal: next !== "branch" || reason === "late-shock" };
  }

  // Move to whatever resolve() decided. Returns "stage" | "death" | "debrief".
  function advance(run) {
    const p = run.pending;
    run.pending = null;
    if (!p) return run.done ? "debrief" : "stage";
    if (p.next === "death") return "death";
    if (p.next === "branch") {
      run.attempt++;
      run.level++;
      run.current = present(run, degrade(baseStage(run), run.level, p.reason));
      return "stage";
    }
    run.stageIdx++;
    run.attempt = 0;
    run.level = 0;
    if (run.stageIdx >= run.scenario.stages.length) {
      run.stageIdx = run.scenario.stages.length - 1;
      run.done = true;
      return "debrief";
    }
    run.current = present(run, baseStage(run));
    return "stage";
  }

  // Outcome line for a resolved decision (vetted stage text + engine notes).
  function outcomeText(run, res) {
    const st = run.current;
    const secs = Math.round((run.timeline[run.timeline.length - 1] || {}).decisionMs / 1000);
    if (res.next === "branch" && res.reason === "late-shock")
      return `⏱ Right call — but it took ${secs} s with no pulse, and the rhythm persisted through the delay. Watch the monitor.`;
    if (res.next === "branch") return "The patient doesn't improve. Watch the monitor.";
    if (res.next === "takeover") return `The team leader steps in and directs it: ${st.intervention}. ${st.outcome}`;
    if (res.late) return `${st.outcome} ⏱ Right call, but it took ${secs} s with no pulse — those seconds count against the patient.`;
    return st.outcome;
  }

  // ---------- debrief: ideal sequence + metrics ----------

  function idealSequence(scenario) {
    return scenario.stages.map((st, i) => ({ step: i, text: st.intervention, critical: isCritical(st), kinds: classify(st.intervention) }));
  }

  // One row per authored step: what the ideal code did vs. what happened.
  //   status: "on-time" | "late" | "recovered" | "team" | "missed" | "not-reached"
  function compare(run) {
    return idealSequence(run.scenario).map((ideal) => {
      const tries = run.timeline.filter((e) => e.stage === ideal.step);
      const first = tries[0];
      let status = "not-reached";
      let t = null;
      if (first) {
        const done = tries.find((e) => e.correct) || tries.find((e) => e.next === "takeover");
        t = (done || tries[tries.length - 1]).t;
        if (first.correct) status = first.late ? "late" : "on-time";
        else if (tries.some((e) => e.correct)) status = "recovered";
        else if (tries.some((e) => e.next === "takeover")) status = "team";
        else status = "missed";
      }
      return { ...ideal, status, t, attempts: tries.length };
    });
  }

  // Time (ms since start) an intervention of a kind was actually carried
  // out — by the learner, or by the team leader after a takeover.
  function timeTo(run, kind) {
    const e = run.timeline.find((x) => (x.correct || x.next === "takeover") && classify(x.text)[kind]);
    return e ? e.t : null;
  }

  function metrics(run) {
    const tl = run.timeline;
    const decided = tl.filter((e) => !e.timedOut);
    return {
      difficulty: run.difficulty,
      timeToFirstShockMs: timeTo(run, "shock"),
      timeToEpiMs: timeTo(run, "epi"),
      timeToCardioversionMs: timeTo(run, "cardioversion"),
      lateDecisions: tl.filter((e) => e.late).length,
      timeouts: tl.filter((e) => e.timedOut).length,
      branches: tl.filter((e) => e.next === "branch").length,
      recovered: run.recovered,
      meanDecisionMs: decided.length ? Math.round(decided.reduce((n, e) => n + e.decisionMs, 0) / decided.length) : null,
    };
  }

  window.EkgScenarioEngine = {
    DIFFICULTY,
    DEFAULT_DIFFICULTY,
    INTERRUPTIONS,
    difficultyConfig,
    isCritical,
    isShockStage,
    classify,
    degrade,
    eliminate,
    buildDistractorPool,
    distractorsFor,
    createRun,
    resolve,
    advance,
    outcomeText,
    timeLimitMs,
    shouldInterrupt,
    pickInterruption,
    idealSequence,
    compare,
    metrics,
  };
})();
