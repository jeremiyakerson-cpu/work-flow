/*
 * Code-scenario (megacode) engine: multi-stage cases where the rhythm on
 * the monitor changes as the code evolves. Left pane = Zoll monitor,
 * right pane = animated patient/code scene with an interventions log.
 */

const SCENARIO_BEST_PREFIX = "ekg-zoll-scenario-best-";

// Clinical focus tags for the authored scenarios (generated ones carry
// their own `focus`); feeds the performance profile the generator uses.
const SCENARIO_FOCUS = {
  "vf-arrest": "shockable-arrest",
  "unstable-brady": "bradycardia",
  "svt-crash": "tachycardia",
  "torsades-arrest": "torsades-qt",
  "pea-cause": "nonshockable-arrest",
  "found-down-asystole": "nonshockable-arrest",
  "afib-rvr-crash": "tachycardia",
  "stemi-vt": "stable-vt-acs",
  "hyperk-code": "nonshockable-arrest",
  "tension-pneumo": "nonshockable-arrest",
  "wpw-afib": "tachycardia",
  "mobitz2-anterior": "bradycardia",
  "dig-toxicity": "bradycardia",
  "stemi-vf": "shockable-arrest",
  "opioid-arrest": "nonshockable-arrest",
  "massive-pe": "nonshockable-arrest",
};

const SCENARIO_DIFFICULTY_KEY = "ekg-scenario-difficulty";

// Run state. The adaptive logic (branching, difficulty, timeline) lives in
// EkgScenarioEngine; this holds the engine run plus UI-only bits.
// stageIdx mirrors run.stageIdx for callers outside this file.
const scState = {
  scenario: null,
  run: null,
  stageIdx: 0,
  difficulty: null,
  startTime: null,
  elapsedId: null,
  decisionStart: null, // when the current decision was posed
  countdownId: null,
  deathShown: false,
  logged: false, // this run already written to the code log
  stageAnswered: false, // current stage already graded (blocks double taps)
  lastRhythm: null,
};

function scStageCritical(st) {
  return EkgScenarioEngine.isCritical(st);
}

function scDifficulty() {
  if (scState.difficulty) return scState.difficulty;
  const saved = scStoreGet(SCENARIO_DIFFICULTY_KEY);
  return EkgScenarioEngine.DIFFICULTY[saved] ? saved : EkgScenarioEngine.DEFAULT_DIFFICULTY;
}

function scSetDifficulty(key) {
  if (!EkgScenarioEngine.DIFFICULTY[key]) return;
  scState.difficulty = key;
  try {
    localStorage.setItem(SCENARIO_DIFFICULTY_KEY, key);
  } catch {}
  scRenderDifficulty();
}

function scRenderDifficulty() {
  const cur = scDifficulty();
  document.querySelectorAll("#sc-difficulty .sc-diff-btn").forEach((b) => {
    b.setAttribute("aria-pressed", String(b.dataset.diff === cur));
  });
  const note = document.getElementById("sc-diff-note");
  if (note) note.textContent = EkgScenarioEngine.difficultyConfig(cur).blurb;
}

function scFmtElapsed(ms) {
  const s = Math.floor(ms / 1000);
  return `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;
}

function scStartClock() {
  scState.startTime = Date.now();
  scStopClock();
  const el = document.getElementById("sc-elapsed");
  el.textContent = "ELAPSED 00:00";
  scState.elapsedId = setInterval(() => {
    el.textContent = `ELAPSED ${scFmtElapsed(Date.now() - scState.startTime)}`;
  }, 1000);
}

function scStopClock() {
  if (scState.elapsedId) {
    clearInterval(scState.elapsedId);
    scState.elapsedId = null;
  }
}

function scShuffle(arr) {
  const a = arr.slice();
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

function scShow(id) {
  window.showScreen(id);
}

// ---------- picker ----------

function scStoreGet(key) {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

// Generated cards get a delete control. It sits beside the card button in a
// wrapper (not inside it — a <button> nested in a <button> is invalid HTML
// and unreachable by keyboard / screen readers).
function scMakeCard(s, opts = {}) {
  const best = scStoreGet(SCENARIO_BEST_PREFIX + s.id);
  const card = document.createElement("button");
  card.type = "button";
  card.className = `sc-card ${opts.generated ? "sc-card-generated" : ""}`;
  card.innerHTML = `
    <span class="sc-card-title">${s.title}</span>
    <span class="sc-card-blurb">${s.blurb}</span>
    ${opts.generated && s.reason ? `<span class="sc-card-reason">${s.reason}</span>` : ""}
    <span class="sc-card-meta">
      <span class="sc-card-stages">${s.stages.length} decisions${opts.generated ? " · GENERATED" : ""}</span>
      ${best !== null ? `<span class="sc-card-best">BEST ${best}/${s.stages.length}</span>` : `<span class="sc-card-new">NOT ATTEMPTED</span>`}
    </span>
  `;
  card.addEventListener("click", () => scStart(s));
  if (!opts.generated) return card;

  const wrap = document.createElement("div");
  wrap.className = "sc-card-wrap";
  const del = document.createElement("button");
  del.type = "button";
  del.className = "sc-card-delete";
  del.title = "Delete this generated case";
  del.setAttribute("aria-label", `Delete generated case: ${s.title}`);
  del.textContent = "×";
  del.addEventListener("click", () => {
    EkgGenerator.removeGenerated(s.id);
    try {
      localStorage.removeItem(SCENARIO_BEST_PREFIX + s.id);
    } catch {}
    scOpenPicker();
  });
  wrap.append(card, del);
  return wrap;
}

function scOpenPicker() {
  scStopCountdown();
  scShow("sc-picker");
  scRenderDifficulty();

  const statsEl = document.getElementById("sc-stats-line");
  const line = window.EkgStats ? EkgStats.summaryLine() : null;
  statsEl.textContent =
    line ||
    "Answer questions and run cases — the generator tracks what you miss and creates new cases aimed at your weak areas.";

  const grid = document.getElementById("sc-picker-grid");
  grid.innerHTML = "";
  EKG_SCENARIOS.forEach((s) => grid.appendChild(scMakeCard(s)));

  const genGrid = document.getElementById("sc-generated-grid");
  genGrid.innerHTML = "";

  const genCard = document.createElement("button");
  genCard.type = "button";
  genCard.className = "sc-card sc-generate-card";
  genCard.innerHTML = `
    <span class="sc-card-title">⚡ Generate a new case</span>
    <span class="sc-card-blurb">Composes a fresh megacode from vetted clinical building blocks — new patient, new twists, new cause — weighted toward the areas you miss most.</span>
  `;
  genCard.addEventListener("click", () => {
    const s = EkgGenerator.generate();
    scStart(s);
  });
  genGrid.appendChild(genCard);

  EkgGenerator.listGenerated().forEach((s) => genGrid.appendChild(scMakeCard(s, { generated: true })));
}

// Distractor pool: every stage the app knows (authored + generated library).
function scDistractorPool() {
  const all = EKG_SCENARIOS.concat(window.EkgGenerator ? EkgGenerator.listGenerated() : []);
  return EkgScenarioEngine.buildDistractorPool(all);
}

function scStart(scenario) {
  scStopCountdown();
  scState.scenario = scenario;
  scState.run = EkgScenarioEngine.createRun(scenario, scDifficulty(), { pool: scDistractorPool() });
  scState.stageIdx = 0;
  scState.deathShown = false;
  scState.logged = false;
  scState.lastRhythm = null;
  scShow("sc-shell");
  scStartClock();
  scRenderStage();
}

function scFocusOf() {
  return scState.scenario.focus || SCENARIO_FOCUS[scState.scenario.id] || null;
}

// Write this run to the code log exactly once, whatever way it ended.
function scLogRun(outcome) {
  if (scState.logged || !window.EkgCodeLog) return;
  scState.logged = true;
  const run = scState.run;
  EkgCodeLog.add({
    title: scState.scenario.title,
    scenarioId: scState.scenario.id,
    generated: !!scState.scenario.generated,
    focus: scFocusOf(),
    outcome,
    correct: run.correct,
    answered: new Set(run.timeline.map((e) => e.stage)).size,
    total: scState.scenario.stages.length,
    durationMs: scState.startTime ? Date.now() - scState.startTime : 0,
    misses: run.misses,
    difficulty: run.difficulty,
    metrics: EkgScenarioEngine.metrics(run),
    timeline: run.timeline,
  });
}

// ---------- stage rendering ----------

function scStage() {
  return scState.run.current;
}

function scRenderStage() {
  const s = scState.scenario;
  const run = scState.run;
  const st = scStage();
  scState.stageIdx = run.stageIdx;

  document.getElementById("sc-title").textContent = s.title;
  document.getElementById("sc-progress").textContent =
    `Decision ${run.stageIdx + 1} of ${s.stages.length}` + (run.attempt > 0 ? " · the patient is reacting" : "");
  const badge = document.getElementById("sc-diff-badge");
  badge.textContent = run.cfg.label.toUpperCase();
  badge.className = `sc-diff-badge diff-${run.difficulty}`;

  scRenderMonitor(st, { sweep: scState.lastRhythm !== null && scState.lastRhythm !== st.rhythm });
  scState.lastRhythm = st.rhythm;
  scRenderScene(st.scene);
  scRenderLog();

  document.getElementById("sc-narrative").textContent = st.narrative;
  document.getElementById("sc-question").textContent = st.question;

  const rationale = document.getElementById("sc-rationale");
  rationale.hidden = true;
  const outcome = document.getElementById("sc-outcome");
  outcome.hidden = true;
  document.getElementById("sc-next-btn").hidden = true;
  scState.stageAnswered = false;

  scRenderHint(st);

  // choices
  const order = scShuffle(st.choices.map((_, i) => i));
  const correctIndex = order.indexOf(st.answer);
  const container = document.getElementById("sc-softkeys");
  container.innerHTML = "";
  order.forEach((origIdx, displayIdx) => {
    const btn = document.createElement("button");
    btn.className = "softkey";
    btn.innerHTML = `<span class="softkey-letter">${String.fromCharCode(65 + displayIdx)}</span><span class="softkey-text">${st.choices[origIdx]}</span>`;
    btn.addEventListener("click", () => scAnswer(displayIdx, correctIndex, container));
    container.appendChild(btn);
  });
  container.dataset.correctIndex = String(correctIndex);

  scState.decisionStart = Date.now();
  scMaybeInterrupt(container);
  scStartCountdown();
}

// Guided mode: a hint on every decision (existing coaching text only).
function scRenderHint(st) {
  const el = document.getElementById("sc-hint");
  const run = scState.run;
  if (!run.cfg.hints) {
    el.hidden = true;
    el.textContent = "";
    return;
  }
  const bits = [];
  if (scStageCritical(st)) bits.push("No solid pulse — this decision is time-critical.");
  if (st.eliminated) bits.push(`${st.eliminated} wrong option${st.eliminated === 1 ? " has" : "s have"} been removed.`);
  const fh = window.EkgCodeLog ? EkgCodeLog.focusHint(scFocusOf()) : null;
  if (fh) bits.push(`Remember to ${fh}`);
  el.hidden = !bits.length;
  el.textContent = bits.length ? `💡 ${bits.join(" ")}` : "";
}

// Expert mode: a non-clinical interruption that blocks the choices until
// it's handled. The decision clock keeps running.
function scMaybeInterrupt(container) {
  const box = document.getElementById("sc-interrupt");
  const run = scState.run;
  if (!EkgScenarioEngine.shouldInterrupt(run)) {
    box.hidden = true;
    return;
  }
  document.getElementById("sc-interrupt-text").textContent = EkgScenarioEngine.pickInterruption(run);
  box.hidden = false;
  container.classList.add("sc-blocked");
  container.querySelectorAll(".softkey").forEach((b) => (b.disabled = true));
  const btn = document.getElementById("sc-interrupt-btn");
  btn.focus({ preventScroll: true });
}

function scDismissInterrupt() {
  document.getElementById("sc-interrupt").hidden = true;
  const container = document.getElementById("sc-softkeys");
  container.classList.remove("sc-blocked");
  container.querySelectorAll(".softkey").forEach((b) => (b.disabled = false));
  const first = container.querySelector(".softkey");
  if (first) first.focus({ preventScroll: true });
}

// ---------- decision clock (expert) ----------

function scStopCountdown() {
  if (scState.countdownId) {
    clearInterval(scState.countdownId);
    scState.countdownId = null;
  }
  const el = document.getElementById("sc-countdown");
  if (el) el.hidden = true;
}

function scStartCountdown() {
  scStopCountdown();
  const limit = EkgScenarioEngine.timeLimitMs(scState.run);
  if (!limit) return;
  const el = document.getElementById("sc-countdown");
  el.hidden = false;
  const tick = () => {
    const left = limit - (Date.now() - scState.decisionStart);
    el.textContent = `DECIDE ${scFmtElapsed(Math.max(0, left) + 999)}`;
    el.classList.toggle("urgent", left <= 5000);
    if (left <= 0) {
      scStopCountdown();
      scTimeout();
    }
  };
  tick();
  scState.countdownId = setInterval(tick, 250);
}

function scTimeout() {
  if (scState.stageAnswered) return;
  document.getElementById("sc-interrupt").hidden = true;
  scGrade({ timedOut: true, displayIdx: -1 });
}

function scRenderMonitor(st, opts = {}) {
  const v = st.vitals || {};
  const fmt = (x) => (x === null || x === undefined ? "--" : x);
  document.getElementById("sc-vital-hr").textContent = fmt(v.hr);
  document.getElementById("sc-vital-spo2").textContent = fmt(v.spo2);
  document.getElementById("sc-vital-nibp").textContent = v.nibp || "--/--";
  document.getElementById("sc-vital-rr").textContent = fmt(v.rr);
  document.getElementById("sc-vital-etco2").textContent = fmt(v.etco2);

  const alarm = document.getElementById("sc-alarm-banner");
  const lethal = ["vf_coarse", "vf_fine", "asystole", "vt", "torsades"];
  const namesRhythm = !scState.run || scState.run.cfg.alarmNamesRhythm;
  if (lethal.includes(st.rhythm)) {
    alarm.hidden = false;
    alarm.textContent = !namesRhythm
      ? "*** ALARM — CHECK PATIENT ***"
      : st.rhythm === "asystole"
        ? "*** ASYSTOLE — CHECK PATIENT ***"
        : st.rhythm.startsWith("vf")
        ? "*** V-FIB — CHECK PATIENT ***"
        : "*** LETHAL RHYTHM — CHECK PATIENT ***";
  } else {
    alarm.hidden = true;
  }

  if (!window.__scMonitor) window.__scMonitor = EkgMonitor.attach("sc-ekg-canvas");
  window.__scMonitor.setRhythm(st.rhythm, {
    hr: v.hr,
    perfusing: st.scene && st.scene.pulse !== "ABSENT",
    lethal: !alarm.hidden,
    sweep: !!opts.sweep,
  });
}

// ---------- the code scene (animated patient) ----------

const LOC_STYLE = { ALERT: "good", ALTERED: "warn", UNRESPONSIVE: "bad" };
const PULSE_STYLE = { PRESENT: "good", WEAK: "warn", ABSENT: "bad" };
const RESP_STYLE = { SPONTANEOUS: "good", ASSISTED: "warn", NONE: "bad" };

function scRenderScene(scene) {
  const wrap = document.getElementById("sc-scene");
  const breathing = !scene.cpr && scene.breathing === "SPONTANEOUS";
  wrap.className = `sc-scene ${scene.cpr ? "scene-cpr" : ""} ${breathing ? "scene-breathing" : ""}`;

  // The code cart/IV appear whenever a resuscitation is in progress, or
  // when a scenario explicitly flags them.
  const showCart = scene.pads || scene.cpr || scene.bvm;
  const showIv = scene.iv || scene.bvm || scene.cpr;
  const showMedNurse = scene.meds || scene.cpr;

  const ivPole = showIv
    ? `
      <g class="iv-pole">
        <line x1="36" y1="78" x2="36" y2="214" stroke="#55606e" stroke-width="2.5"/>
        <line x1="26" y1="80" x2="46" y2="80" stroke="#55606e" stroke-width="2"/>
        <line x1="36" y1="214" x2="24" y2="226" stroke="#55606e" stroke-width="2.5"/>
        <line x1="36" y1="214" x2="48" y2="226" stroke="#55606e" stroke-width="2.5"/>
        <rect x="21" y="84" width="15" height="23" rx="2" fill="#cfe3f2" opacity="0.92"/>
        <line x1="23" y1="95" x2="34" y2="95" stroke="#8fb0c4" stroke-width="1"/>
        <path d="M 28 107 C 38 142 118 150 150 159" fill="none" stroke="#bcd7e6" stroke-width="1.2" opacity="0.8"/>
      </g>`
    : "";

  const cart = showCart
    ? `
      <g class="crash-cart">
        <rect x="382" y="152" width="56" height="62" rx="5" fill="#6e2f36"/>
        <line x1="385" y1="172" x2="435" y2="172" stroke="#8d4a51" stroke-width="1.5"/>
        <line x1="385" y1="188" x2="435" y2="188" stroke="#8d4a51" stroke-width="1.5"/>
        <line x1="385" y1="203" x2="435" y2="203" stroke="#8d4a51" stroke-width="1.5"/>
        <circle cx="393" cy="219" r="5" fill="#39434f"/>
        <circle cx="428" cy="219" r="5" fill="#39434f"/>
        <rect x="386" y="127" width="46" height="25" rx="3" fill="#262b31"/>
        <rect x="391" y="132" width="17" height="15" fill="#06220f"/>
        <polyline points="392,140 395,140 396,135 398,144 400,140 406,140" fill="none" stroke="#39ff8f" stroke-width="1"/>
        <circle cx="418" cy="135" r="2.4" fill="#f0a94e"/>
        <circle cx="418" cy="143" r="2.4" fill="#39ff8f"/>
      </g>`
    : "";

  const compressorBody = scene.cpr
    ? `
      <g class="compressor">
        <circle cx="146" cy="68" r="6.5" fill="#3b2f28"/>
        <circle cx="149" cy="74" r="10.5" fill="#d4a48d"/>
        <rect x="136" y="86" width="26" height="44" rx="11" fill="#3f8f8a"/>
      </g>`
    : "";

  const compressorArms = scene.cpr
    ? `
      <g class="compressor">
        <path d="M 143 100 L 147 134" stroke="#3f8f8a" stroke-width="6.5" stroke-linecap="round" fill="none"/>
        <path d="M 157 100 L 152 134" stroke="#3f8f8a" stroke-width="6.5" stroke-linecap="round" fill="none"/>
        <ellipse cx="149" cy="138" rx="6" ry="4.5" fill="#d4a48d"/>
      </g>`
    : "";

  const airwayBody = scene.bvm
    ? `
      <g class="bvm">
        <circle cx="69" cy="82" r="6" fill="#2e2620"/>
        <circle cx="72" cy="88" r="10" fill="#b98a70"/>
        <rect x="60" y="98" width="24" height="40" rx="10" fill="#7a6fae"/>
        <path d="M 70 118 C 80 128 90 134 98 140" stroke="#7a6fae" stroke-width="6" stroke-linecap="round" fill="none"/>
        <path d="M 64 120 C 70 127 76 130 82 131" stroke="#7a6fae" stroke-width="6" stroke-linecap="round" fill="none"/>
      </g>`
    : "";

  const airwayFront = scene.bvm
    ? `
      <g class="bvm">
        <ellipse class="bag" cx="88" cy="128" rx="10" ry="7" fill="#cfe3f2"/>
        <line x1="95" y1="132" x2="100" y2="139" stroke="#d7e4ee" stroke-width="2.5"/>
        <ellipse cx="103" cy="143" rx="6.5" ry="5" fill="#d7e4ee" opacity="0.95"/>
      </g>`
    : "";

  const medNurse = showMedNurse
    ? `
      <g class="med-nurse">
        <circle cx="357" cy="108" r="6" fill="#463229"/>
        <circle cx="360" cy="113" r="9.5" fill="#caa27f"/>
        <rect x="350" y="124" width="21" height="40" rx="10" fill="#a8718a"/>
        <path d="M 356 140 C 366 146 374 148 381 150" stroke="#a8718a" stroke-width="5.5" stroke-linecap="round" fill="none"/>
        <rect x="373" y="145" width="9" height="3.5" rx="1.5" fill="#dfe6ee"/>
      </g>`
    : "";

  const pads = scene.pads
    ? `
      <g class="pads">
        <rect x="133" y="139" width="15" height="10" rx="3" fill="#f0a94e"/>
        <rect x="166" y="153" width="16" height="10" rx="3" fill="#f0a94e"/>
        <path d="M 141 139 C 200 92 330 110 398 132" fill="none" stroke="#f0a94e" stroke-width="1.6" opacity="0.55"/>
        <path d="M 174 153 C 240 120 340 122 398 138" fill="none" stroke="#f0a94e" stroke-width="1.6" opacity="0.55"/>
      </g>`
    : "";

  wrap.innerHTML = `
    <svg class="scene-svg" viewBox="0 0 460 235" role="img" aria-label="Simulated code scene — training illustration">
      <ellipse cx="213" cy="224" rx="172" ry="8" fill="#000" opacity="0.35"/>
      ${ivPole}
      ${cart}
      ${compressorBody}
      ${airwayBody}
      <!-- stretcher -->
      <rect x="58" y="158" width="310" height="13" rx="4" fill="#26303d" stroke="#323e4d" stroke-width="1"/>
      <rect x="64" y="171" width="298" height="5" fill="#1b232d"/>
      <rect x="96" y="176" width="6" height="36" fill="#1b232d"/>
      <rect x="326" y="176" width="6" height="36" fill="#1b232d"/>
      <circle cx="99" cy="216" r="6" fill="#39434f"/>
      <circle cx="329" cy="216" r="6" fill="#39434f"/>
      <!-- patient -->
      <g class="patient">
        <circle cx="100" cy="140" r="7" fill="#4a3b32"/>
        <circle cx="104" cy="147" r="12" fill="#c9a08a"/>
        <rect x="114" y="150" width="8" height="8" fill="#c9a08a"/>
        <rect class="torso" x="119" y="137" width="116" height="27" rx="12" fill="#4f6f96"/>
        <rect x="126" y="157" width="24" height="10" rx="5" fill="#46618a"/>
        <rect x="148" y="158" width="42" height="8" rx="4" fill="#c9a08a"/>
        <circle cx="193" cy="162" r="4.5" fill="#c9a08a"/>
        <rect x="233" y="140" width="112" height="22" rx="9" fill="#62788e"/>
        <circle cx="346" cy="148" r="8" fill="#62788e"/>
        <!-- ECG electrodes & leads -->
        <path d="M 139 143 C 96 112 44 96 0 90" fill="none" stroke="#9aa5b1" stroke-width="1" opacity="0.5"/>
        <path d="M 129 152 C 90 126 40 108 0 102" fill="none" stroke="#9aa5b1" stroke-width="1" opacity="0.5"/>
        <path d="M 151 153 C 104 130 46 118 0 114" fill="none" stroke="#9aa5b1" stroke-width="1" opacity="0.5"/>
        <circle cx="139" cy="143" r="2.6" fill="#e9edf2"/>
        <circle cx="129" cy="152" r="2.6" fill="#2c2c2c" stroke="#555" stroke-width="0.7"/>
        <circle cx="151" cy="153" r="2.6" fill="#ff5050"/>
      </g>
      ${pads}
      ${compressorArms}
      ${airwayFront}
      ${medNurse}
    </svg>
    <div class="sc-badges">
      <span class="sc-badge ${LOC_STYLE[scene.loc]}">LOC: ${scene.loc}</span>
      <span class="sc-badge ${PULSE_STYLE[scene.pulse]}">PULSE: ${scene.pulse}</span>
      <span class="sc-badge ${RESP_STYLE[scene.breathing]}">RESP: ${scene.breathing}</span>
      ${scene.cpr ? '<span class="sc-badge cpr-chip">CPR IN PROGRESS</span>' : ""}
    </div>
  `;
}

function scLogTags(e) {
  const tags = [];
  if (e.timedOut) tags.push("TIMED OUT");
  if (e.late) tags.push("LATE");
  if (e.attempt > 0) tags.push("2ND TRY");
  if (e.next === "takeover") tags.push("TEAM LEADER");
  return tags.map((t) => `<span class="sc-log-tag">${t}</span>`).join("");
}

function scRenderLog() {
  const list = document.getElementById("sc-log");
  const tl = scState.run ? scState.run.timeline : [];
  if (tl.length === 0) {
    list.innerHTML = `<li class="sc-log-empty">No interventions yet</li>`;
    return;
  }
  list.innerHTML = tl
    .map((e) => `<li class="${e.correct ? "log-ok" : "log-miss"}">${e.correct ? "✓" : "✗"} ${e.text} ${scLogTags(e)}</li>`)
    .join("");
}

// ---------- answering & flow ----------

function scAnswer(displayIdx, correctIndex, container) {
  if (scState.stageAnswered) return; // double tap / second choice
  scGrade({ displayIdx, correct: displayIdx === correctIndex });
}

// Grade the current decision (a tap, or the expert clock running out) and
// show what happens to the patient.
function scGrade({ displayIdx, correct, timedOut = false }) {
  if (scState.stageAnswered) return;
  scState.stageAnswered = true;
  scStopCountdown();
  const run = scState.run;
  const st = scStage();
  const now = Date.now();
  const res = EkgScenarioEngine.resolve(run, {
    correct,
    timedOut,
    atMs: now - scState.startTime,
    decisionMs: now - scState.decisionStart,
  });

  if (window.EkgStats && run.attempt === 0) {
    EkgStats.record({
      kind: "scenario",
      category: null,
      rhythm: st.rhythm,
      focus: scFocusOf(),
      correct: res.correct,
    });
  }

  const container = document.getElementById("sc-softkeys");
  const correctIndex = Number(container.dataset.correctIndex);
  container.querySelectorAll(".softkey").forEach((btn, i) => {
    btn.disabled = false;
    btn.classList.add("disabled");
    btn.setAttribute("aria-disabled", "true");
    if (i === correctIndex && res.reveal) btn.classList.add("correct");
    else if (i === displayIdx && !res.correct) btn.classList.add("incorrect");
  });

  const rationale = document.getElementById("sc-rationale");
  if (res.reveal) {
    rationale.hidden = false;
    rationale.className = `rationale ${res.correct ? "is-correct" : "is-incorrect"}`;
    const head = res.correct ? "Correct." : timedOut ? "Time ran out." : "Not quite.";
    rationale.innerHTML = `<strong>${head}</strong> ${st.rationale}`;
  } else {
    // A branch re-poses this decision: keep the answer hidden so the
    // second attempt is still a real decision.
    rationale.hidden = false;
    rationale.className = "rationale is-incorrect";
    rationale.innerHTML = `<strong>${timedOut ? "Time ran out." : "Not quite."}</strong> The answer stays hidden — you'll get another chance as the patient's condition changes.`;
  }

  const outcome = document.getElementById("sc-outcome");
  outcome.hidden = false;
  const limit = run.cfg.criticalLimit;
  if (run.death) {
    outcome.textContent =
      "The delay is one too many. The rhythm on the monitor degrades and the ETCO2 falls — the patient is slipping away despite the team's efforts.";
  } else if (!res.correct && res.critical && run.criticalMisses === limit - 1) {
    outcome.textContent =
      EkgScenarioEngine.outcomeText(run, res) +
      " ⚠ That miss cost precious seconds with no pulse — another critical miss and this patient may not be recoverable.";
  } else {
    outcome.textContent = EkgScenarioEngine.outcomeText(run, res);
  }

  scRenderLog();

  const nextBtn = document.getElementById("sc-next-btn");
  nextBtn.hidden = false;
  nextBtn.textContent = run.death
    ? "Continue →"
    : res.next === "branch"
    ? "See what happens →"
    : run.stageIdx === scState.scenario.stages.length - 1
    ? "Debrief →"
    : "Continue the code →";
  nextBtn.focus({ preventScroll: true });
  revealBelow(nextBtn); // ekg-test.js: scroll the rationale/outcome into view on phones
}

function scNext() {
  if (!scState.stageAnswered) return;
  const run = scState.run;
  if (run.death && !scState.deathShown) {
    scState.deathShown = true;
    scRenderDeath();
    return;
  }
  if (run.death) {
    scDebrief("died");
    return;
  }
  const where = EkgScenarioEngine.advance(run);
  if (where === "debrief") {
    scDebrief("completed");
    return;
  }
  scRenderStage();
}

// Terminal sequence after two critical misses: the monitor goes to
// asystole, the team stands down, and the run proceeds to debrief.
function scRenderDeath() {
  document.getElementById("sc-progress").textContent = "Resuscitation terminated";

  scRenderMonitor({
    sweep: true,
    rhythm: "asystole",
    vitals: { hr: 0, spo2: null, nibp: "--/--", rr: 0, etco2: null },
    scene: { pulse: "ABSENT" },
  });
  scRenderScene({ cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "NONE" });
  scRenderLog();

  document.getElementById("sc-narrative").textContent =
    "Cycles continue, but the missed windows compound: the rhythm degrades to asystole and never returns. After confirming no reversible cause remains, the team leader calls it. Time of death recorded.";
  document.getElementById("sc-question").textContent =
    "In the simulator, every lost patient is a free lesson — the debrief shows exactly which decisions to take back.";
  document.getElementById("sc-softkeys").innerHTML = "";
  document.getElementById("sc-hint").hidden = true;
  document.getElementById("sc-interrupt").hidden = true;
  document.getElementById("sc-rationale").hidden = true;
  const outcome = document.getElementById("sc-outcome");
  outcome.hidden = true;

  const nextBtn = document.getElementById("sc-next-btn");
  nextBtn.hidden = false;
  nextBtn.textContent = "Debrief →";
}

const IDEAL_STATUS = {
  "on-time": { icon: "✓", label: "on time", cls: "log-ok" },
  late: { icon: "⏱", label: "late", cls: "log-late" },
  recovered: { icon: "↻", label: "2nd try", cls: "log-late" },
  team: { icon: "✗", label: "team leader", cls: "log-miss" },
  missed: { icon: "✗", label: "missed", cls: "log-miss" },
  "not-reached": { icon: "–", label: "not reached", cls: "log-skip" },
};

function scDebrief(outcome = "completed") {
  scStopCountdown();
  const s = scState.scenario;
  const run = scState.run;
  const total = s.stages.length;
  if (outcome === "completed") {
    const key = SCENARIO_BEST_PREFIX + s.id;
    const best = Number(scStoreGet(key) ?? -1);
    if (run.correct > best) {
      try {
        localStorage.setItem(key, String(run.correct));
      } catch {}
    }
  }

  scStopClock();
  scLogRun(outcome);
  scShow("sc-debrief");
  document.getElementById("sc-debrief-title").textContent = s.title;
  document.getElementById("sc-debrief-score").textContent = `${run.correct} / ${total}`;
  document.getElementById("sc-debrief-time").textContent = scFmtElapsed(Date.now() - scState.startTime);

  const banner = document.getElementById("sc-debrief-banner");
  if (outcome === "died") {
    banner.hidden = false;
    banner.className = "exam-banner exam-fail";
    banner.innerHTML = `<strong>PATIENT LOST</strong> — too many critical decisions were missed while the patient had no solid pulse.`;
  } else {
    banner.hidden = true;
  }

  const verdict = document.getElementById("sc-debrief-verdict");
  if (outcome === "died")
    verdict.textContent = "In the simulator this is a free lesson. The pointers below are the decisions to take back — then run it again.";
  else if (run.correct === total) verdict.textContent = "Flawless code — every decision on the first try.";
  else if (run.correct >= total - 1) verdict.textContent = "Strong run — one decision to review below.";
  else verdict.textContent = "The patient made it because the team backstopped the misses — review the pointers below.";

  scRenderMetrics(run);

  // pointers from this run's misses
  const ptsEl = document.getElementById("sc-debrief-pointers");
  if (run.misses.length && window.EkgCodeLog) {
    const pts = EkgCodeLog.pointersFor({ misses: run.misses });
    ptsEl.hidden = false;
    ptsEl.innerHTML =
      `<h3>Pointers from this run</h3>` +
      pts.map((p) => `<div class="codelog-pointer ${p.critical ? "pointer-critical" : ""}">${p.critical ? "⛔" : "•"} ${p.text}</div>`).join("");
  } else {
    ptsEl.hidden = true;
    ptsEl.innerHTML = "";
  }

  // your code vs. the ideal sequence (the scenario's keyed steps, in order)
  document.getElementById("sc-debrief-ideal").innerHTML = EkgScenarioEngine.compare(run)
    .map((row) => {
      const meta = IDEAL_STATUS[row.status];
      const when = row.t != null ? scFmtElapsed(row.t) : "--:--";
      return `<li class="${meta.cls}"><span class="sc-ideal-time">${when}</span><span class="sc-ideal-step">${row.text}</span><span class="sc-ideal-status">${meta.icon} ${meta.label}</span></li>`;
    })
    .join("");

  // the full timeline: every decision, including branch re-poses
  document.getElementById("sc-debrief-log").innerHTML = run.timeline
    .map(
      (e) =>
        `<li class="${e.correct ? "log-ok" : "log-miss"}"><span class="sc-log-step">${scFmtElapsed(e.t)}</span> ${e.correct ? "✓" : "✗"} ${e.text} ${scLogTags(e)}</li>`
    )
    .join("");

  // The library grows itself: every ended run seeds a fresh generated case.
  const fresh = EkgGenerator.generate();
  scState.freshCase = fresh;
  const freshEl = document.getElementById("sc-debrief-fresh");
  freshEl.hidden = false;
  document.getElementById("sc-debrief-fresh-title").textContent = fresh.title;
  document.getElementById("sc-debrief-fresh-reason").textContent = fresh.reason || "";
}

function scRenderMetrics(run) {
  const m = EkgScenarioEngine.metrics(run);
  const fmt = (ms) => (ms == null ? "—" : scFmtElapsed(ms));
  const cells = [
    ["DIFFICULTY", run.cfg.label],
    ["TIME TO 1ST SHOCK", fmt(m.timeToFirstShockMs)],
    ["TIME TO EPI", fmt(m.timeToEpiMs)],
  ];
  if (m.timeToCardioversionMs != null) cells.push(["TIME TO CARDIOVERSION", fmt(m.timeToCardioversionMs)]);
  cells.push(["AVG DECISION", m.meanDecisionMs == null ? "—" : `${Math.round(m.meanDecisionMs / 1000)} s`]);
  cells.push(["LATE / TIMED OUT", `${m.lateDecisions} / ${m.timeouts}`]);
  cells.push(["BRANCHES", String(m.branches)]);
  document.getElementById("sc-debrief-metrics").innerHTML = cells
    .map(([k, v]) => `<div class="sc-metric"><span class="sc-metric-value">${v}</span><span class="sc-metric-label">${k}</span></div>`)
    .join("");
}

// ---------- wiring ----------

function scInit() {
  document.getElementById("mode-scenario").addEventListener("click", scOpenPicker);
  document.getElementById("sc-picker-back").addEventListener("click", () => scShow("start-screen"));
  document.querySelectorAll("#sc-difficulty .sc-diff-btn").forEach((b) =>
    b.addEventListener("click", () => scSetDifficulty(b.dataset.diff))
  );
  document.getElementById("sc-interrupt-btn").addEventListener("click", scDismissInterrupt);
  document.getElementById("sc-exit-btn").addEventListener("click", () => {
    scStopClock();
    scStopCountdown();
    // An exited run with at least one decision made still counts: it goes
    // to the log and still seeds a fresh generated case.
    if (scState.run && scState.run.timeline.length > 0 && !scState.logged) {
      // Leaving after the patient was already lost is still a loss.
      scLogRun(scState.run.death ? "died" : "abandoned");
      EkgGenerator.generate();
    }
    scOpenPicker();
  });
  document.getElementById("sc-next-btn").addEventListener("click", scNext);
  document.getElementById("sc-debrief-retry").addEventListener("click", () => scStart(scState.scenario));
  document.getElementById("sc-debrief-back").addEventListener("click", scOpenPicker);
  document.getElementById("sc-debrief-runnew").addEventListener("click", () => {
    if (scState.freshCase) scStart(scState.freshCase);
  });
}

scInit();
