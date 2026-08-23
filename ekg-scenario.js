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
  document.getElementById("sc-vital-etco2").textContent = fmt(v.etco2);

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
        <rect x="119" y="137" width="116" height="27" rx="12" fill="#4f6f96"/>
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
