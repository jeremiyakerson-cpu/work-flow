/*
 * Code-scenario (megacode) engine: multi-stage cases where the rhythm on
 * the monitor changes as the code evolves. Left pane = Zoll monitor,
 * right pane = animated patient/code scene with an interventions log.
 */

const SCENARIO_BEST_PREFIX = "ekg-zoll-scenario-best-";

const scState = {
  scenario: null,
  stageIdx: 0,
  correct: 0,
  log: [], // { text, correct }
};

function scShuffle(arr) {
  const a = arr.slice();
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

function scShow(id) {
  ["start-screen", "test-shell", "results-screen", "sc-picker", "sc-shell", "sc-debrief"].forEach((elId) => {
    const el = document.getElementById(elId);
    if (el) el.hidden = elId !== id;
  });
}

// ---------- picker ----------

function scOpenPicker() {
  scShow("sc-picker");
  const grid = document.getElementById("sc-picker-grid");
  grid.innerHTML = "";
  EKG_SCENARIOS.forEach((s) => {
    const best = localStorage.getItem(SCENARIO_BEST_PREFIX + s.id);
    const card = document.createElement("button");
    card.className = "sc-card";
    card.innerHTML = `
      <span class="sc-card-title">${s.title}</span>
      <span class="sc-card-blurb">${s.blurb}</span>
      <span class="sc-card-meta">
        <span class="sc-card-stages">${s.stages.length} decisions</span>
        ${best !== null ? `<span class="sc-card-best">BEST ${best}/${s.stages.length}</span>` : `<span class="sc-card-new">NOT ATTEMPTED</span>`}
      </span>
    `;
    card.addEventListener("click", () => scStart(s));
    grid.appendChild(card);
  });
}

function scStart(scenario) {
  scState.scenario = scenario;
  scState.stageIdx = 0;
  scState.correct = 0;
  scState.log = [];
  scShow("sc-shell");
  scRenderStage();
}

// ---------- stage rendering ----------

function scStage() {
  return scState.scenario.stages[scState.stageIdx];
}

function scRenderStage() {
  const s = scState.scenario;
  const st = scStage();

  document.getElementById("sc-title").textContent = s.title;
  document.getElementById("sc-progress").textContent = `Decision ${scState.stageIdx + 1} of ${s.stages.length}`;

  scRenderMonitor(st);
  scRenderScene(st.scene);
  scRenderLog();

  document.getElementById("sc-narrative").textContent = st.narrative;
  document.getElementById("sc-question").textContent = st.question;

  const rationale = document.getElementById("sc-rationale");
  rationale.hidden = true;
  const outcome = document.getElementById("sc-outcome");
  outcome.hidden = true;
  document.getElementById("sc-next-btn").hidden = true;

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
}

function scRenderMonitor(st) {
  const v = st.vitals || {};
  const fmt = (x) => (x === null || x === undefined ? "--" : x);
  document.getElementById("sc-vital-hr").textContent = fmt(v.hr);
  document.getElementById("sc-vital-spo2").textContent = fmt(v.spo2);
  document.getElementById("sc-vital-nibp").textContent = v.nibp || "--/--";
  document.getElementById("sc-vital-rr").textContent = fmt(v.rr);

  const alarm = document.getElementById("sc-alarm-banner");
  const lethal = ["vf_coarse", "vf_fine", "asystole", "vt", "torsades"];
  if (lethal.includes(st.rhythm)) {
    alarm.hidden = false;
    alarm.textContent =
      st.rhythm === "asystole"
        ? "*** ASYSTOLE — CHECK PATIENT ***"
        : st.rhythm.startsWith("vf")
        ? "*** V-FIB — CHECK PATIENT ***"
        : "*** LETHAL RHYTHM — CHECK PATIENT ***";
  } else {
    alarm.hidden = true;
  }

  const canvas = document.getElementById("sc-ekg-canvas");
  const ctx = canvas.getContext("2d");
  const dpr = window.devicePixelRatio || 1;
  const cssW = canvas.clientWidth || 560;
  const cssH = canvas.clientHeight || 150;
  canvas.width = cssW * dpr;
  canvas.height = cssH * dpr;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

  EkgDraw.drawGrid(ctx, cssW, cssH);
  EkgDraw.drawTrace(ctx, EkgRhythms.synthesizeRhythm(st.rhythm), cssW, cssH);
}

// ---------- the code scene (animated patient) ----------

const LOC_STYLE = { ALERT: "good", ALTERED: "warn", UNRESPONSIVE: "bad" };
const PULSE_STYLE = { PRESENT: "good", WEAK: "warn", ABSENT: "bad" };
const RESP_STYLE = { SPONTANEOUS: "good", ASSISTED: "warn", NONE: "bad" };

function scRenderScene(scene) {
  const wrap = document.getElementById("sc-scene");
  wrap.className = `sc-scene ${scene.cpr ? "scene-cpr" : ""}`;

  const pads = scene.pads
    ? `
      <g class="pads">
        <rect x="138" y="143" width="15" height="10" rx="3" fill="#f0a94e"/>
        <rect x="172" y="152" width="15" height="10" rx="3" fill="#f0a94e"/>
        <polyline points="146,143 160,96 300,60 420,52" fill="none" stroke="#f0a94e" stroke-width="1.5" opacity="0.5"/>
        <polyline points="180,152 210,100 320,68 420,62" fill="none" stroke="#f0a94e" stroke-width="1.5" opacity="0.5"/>
      </g>`
    : "";

  const compressor = scene.cpr
    ? `
      <g class="compressor">
        <circle cx="152" cy="66" r="11" fill="#4a90a4"/>
        <rect x="141" y="78" width="22" height="34" rx="9" fill="#4a90a4"/>
        <path d="M 146 104 L 148 134 L 156 134 L 158 104 Z" fill="#4a90a4"/>
        <rect x="143" y="132" width="18" height="7" rx="3" fill="#3a7386"/>
      </g>`
    : "";

  const bvm = scene.bvm
    ? `
      <g class="bvm">
        <circle cx="48" cy="104" r="9" fill="#5b8aa6"/>
        <rect x="40" y="114" width="17" height="26" rx="7" fill="#5b8aa6"/>
        <ellipse class="bag" cx="72" cy="130" rx="11" ry="8" fill="#8fb8cc"/>
        <path d="M 82 134 L 92 142 L 88 148 Z" fill="#8fb8cc"/>
      </g>`
    : "";

  wrap.innerHTML = `
    <svg class="scene-svg" viewBox="0 0 420 235" role="img" aria-label="Simulated code scene — training illustration">
      <rect x="0" y="0" width="420" height="235" fill="transparent"/>
      <!-- stretcher -->
      <rect x="46" y="168" width="330" height="13" rx="6" fill="#1e2631" stroke="#2c3644" stroke-width="1"/>
      <rect x="76" y="181" width="6" height="30" fill="#2c3644"/>
      <rect x="336" y="181" width="6" height="30" fill="#2c3644"/>
      <circle cx="79" cy="215" r="6" fill="#2c3644"/>
      <circle cx="339" cy="215" r="6" fill="#2c3644"/>
      <!-- patient (supine silhouette) -->
      <g class="patient">
        <circle cx="97" cy="152" r="13" fill="#7f93a8"/>
        <rect x="110" y="141" width="128" height="24" rx="11" fill="#7f93a8"/>
        <rect x="236" y="146" width="112" height="15" rx="7" fill="#74879b"/>
        <rect x="124" y="162" width="86" height="8" rx="4" fill="#6d8093"/>
      </g>
      ${pads}
      ${compressor}
      ${bvm}
    </svg>
    <div class="sc-badges">
      <span class="sc-badge ${LOC_STYLE[scene.loc]}">LOC: ${scene.loc}</span>
      <span class="sc-badge ${PULSE_STYLE[scene.pulse]}">PULSE: ${scene.pulse}</span>
      <span class="sc-badge ${RESP_STYLE[scene.breathing]}">RESP: ${scene.breathing}</span>
      ${scene.cpr ? '<span class="sc-badge cpr-chip">CPR IN PROGRESS</span>' : ""}
    </div>
  `;
}

function scRenderLog() {
  const list = document.getElementById("sc-log");
  if (scState.log.length === 0) {
    list.innerHTML = `<li class="sc-log-empty">No interventions yet</li>`;
    return;
  }
  list.innerHTML = scState.log
    .map((e) => `<li class="${e.correct ? "log-ok" : "log-miss"}">${e.correct ? "✓" : "✗"} ${e.text}</li>`)
    .join("");
}

// ---------- answering & flow ----------

function scAnswer(displayIdx, correctIndex, container) {
  const st = scStage();
  const isCorrect = displayIdx === correctIndex;
  if (isCorrect) scState.correct++;

  const btns = container.querySelectorAll(".softkey");
  btns.forEach((btn, i) => {
    btn.classList.add("disabled");
    if (i === correctIndex) btn.classList.add("correct");
    else if (i === displayIdx) btn.classList.add("incorrect");
  });

  const rationale = document.getElementById("sc-rationale");
  rationale.hidden = false;
  rationale.className = `rationale ${isCorrect ? "is-correct" : "is-incorrect"}`;
  rationale.innerHTML = `<strong>${isCorrect ? "Correct." : "Not quite."}</strong> ${st.rationale}`;

  const outcome = document.getElementById("sc-outcome");
  outcome.hidden = false;
  outcome.textContent = st.outcome;

  scState.log.push({ text: st.intervention, correct: isCorrect });
  scRenderLog();

  const nextBtn = document.getElementById("sc-next-btn");
  nextBtn.hidden = false;
  nextBtn.textContent = scState.stageIdx === scState.scenario.stages.length - 1 ? "Debrief →" : "Continue the code →";
}

function scNext() {
  if (scState.stageIdx === scState.scenario.stages.length - 1) {
    scDebrief();
    return;
  }
  scState.stageIdx++;
  scRenderStage();
}

function scDebrief() {
  const s = scState.scenario;
  const total = s.stages.length;
  const key = SCENARIO_BEST_PREFIX + s.id;
  const best = Number(localStorage.getItem(key) ?? -1);
  if (scState.correct > best) localStorage.setItem(key, String(scState.correct));

  scShow("sc-debrief");
  document.getElementById("sc-debrief-title").textContent = s.title;
  document.getElementById("sc-debrief-score").textContent = `${scState.correct} / ${total}`;

  const verdict = document.getElementById("sc-debrief-verdict");
  if (scState.correct === total) verdict.textContent = "Flawless code — every decision on the first try.";
  else if (scState.correct >= total - 1) verdict.textContent = "Strong run — one decision to review below.";
  else verdict.textContent = "The patient made it because the team backstopped the misses — review the log below.";

  document.getElementById("sc-debrief-log").innerHTML = scState.log
    .map(
      (e, i) =>
        `<li class="${e.correct ? "log-ok" : "log-miss"}"><span class="sc-log-step">${i + 1}</span> ${e.correct ? "✓" : "✗"} ${e.text}</li>`
    )
    .join("");
}

// ---------- wiring ----------

function scInit() {
  document.getElementById("mode-scenario").addEventListener("click", scOpenPicker);
  document.getElementById("sc-picker-back").addEventListener("click", () => scShow("start-screen"));
  document.getElementById("sc-exit-btn").addEventListener("click", scOpenPicker);
  document.getElementById("sc-next-btn").addEventListener("click", scNext);
  document.getElementById("sc-debrief-retry").addEventListener("click", () => scStart(scState.scenario));
  document.getElementById("sc-debrief-back").addEventListener("click", scOpenPicker);
}

scInit();
