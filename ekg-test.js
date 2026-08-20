const BEST_SCORE_KEY = "ekg-zoll-trainer-best-score";

const CATEGORY_LABEL = {
  rhythm: "RHYTHM ID",
  meds: "MEDICATIONS",
  condition: "CHANGE IN CONDITION",
  code: "CODE RHYTHM",
};

const state = {
  order: [], // question indices in play order
  current: 0,
  answered: [], // per-question: { chosen, correct, shuffledChoices, correctIndex }
  score: 0,
  started: false,
};

function shuffle(arr) {
  const a = arr.slice();
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

function buildChoiceOrder(question) {
  const idx = question.choices.map((_, i) => i);
  const shuffled = shuffle(idx);
  return {
    order: shuffled,
    correctIndex: shuffled.indexOf(question.answer),
  };
}

function init() {
  state.order = EKG_QUESTIONS.map((_, i) => i);
  state.current = 0;
  state.answered = new Array(EKG_QUESTIONS.length).fill(null);
  state.score = 0;

  document.getElementById("start-btn").addEventListener("click", startTest);
  document.getElementById("retry-btn").addEventListener("click", restart);
  document.getElementById("next-btn").addEventListener("click", nextQuestion);
  tickClock();
  setInterval(tickClock, 1000);
}

function tickClock() {
  const el = document.getElementById("monitor-clock");
  if (!el) return;
  const now = new Date();
  el.textContent = now.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" });
}

function startTest() {
  state.started = true;
  document.getElementById("start-screen").hidden = true;
  document.getElementById("test-shell").hidden = false;
  renderQuestion();
}

function restart() {
  state.current = 0;
  state.answered = new Array(EKG_QUESTIONS.length).fill(null);
  state.score = 0;
  document.getElementById("results-screen").hidden = true;
  document.getElementById("test-shell").hidden = false;
  renderQuestion();
}

function currentQuestion() {
  return EKG_QUESTIONS[state.order[state.current]];
}

function renderQuestion() {
  const q = currentQuestion();
  const qNum = state.current + 1;

  document.getElementById("q-num").textContent = qNum;
  document.getElementById("test-progress-fill").style.width = `${((qNum - 1) / EKG_QUESTIONS.length) * 100}%`;
  document.getElementById("score-live").textContent = state.score;
  document.getElementById("answered-live").textContent = state.current;

  document.getElementById("q-category").textContent = CATEGORY_LABEL[q.category] || q.category.toUpperCase();
  document.getElementById("q-category").className = `q-category cat-${q.category}`;
  document.getElementById("q-stem").textContent = q.stem;

  renderMonitor(q);
  renderChoices(q);

  document.getElementById("rationale").hidden = true;
  document.getElementById("next-btn").hidden = true;
}

function fmtVital(v) {
  return v === null || v === undefined ? "--" : v;
}

function renderMonitor(q) {
  const v = q.vitals || {};
  document.getElementById("vital-hr").textContent = fmtVital(v.hr);
  document.getElementById("vital-spo2").textContent = fmtVital(v.spo2);
  document.getElementById("vital-nibp").textContent = v.nibp || "--/--";
  document.getElementById("vital-rr").textContent = fmtVital(v.rr);

  const alarm = document.getElementById("alarm-banner");
  const lethal = ["vf_coarse", "vf_fine", "asystole", "vt", "torsades"];
  if (lethal.includes(q.rhythm) && (v.hr === null || v.hr === 0 || v.hr >= 150)) {
    alarm.hidden = false;
    alarm.textContent =
      q.rhythm === "asystole"
        ? "*** ASYSTOLE — CHECK PATIENT ***"
        : q.rhythm.startsWith("vf")
        ? "*** V-FIB — CHECK PATIENT ***"
        : "*** LETHAL RHYTHM — CHECK PATIENT ***";
  } else {
    alarm.hidden = true;
  }

  document.getElementById("trace-rhythm-tag").textContent = "";

  const canvas = document.getElementById("ekg-canvas");
  const ctx = canvas.getContext("2d");
  const dpr = window.devicePixelRatio || 1;
  const cssW = canvas.clientWidth || 900;
  const cssH = canvas.clientHeight || 200;
  canvas.width = cssW * dpr;
  canvas.height = cssH * dpr;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

  drawGrid(ctx, cssW, cssH);

  const samples = EkgRhythms.synthesizeRhythm(q.rhythm);
  drawTrace(ctx, samples, cssW, cssH);
}

function drawGrid(ctx, w, h) {
  ctx.fillStyle = "#020a06";
  ctx.fillRect(0, 0, w, h);
  ctx.strokeStyle = "rgba(0, 200, 110, 0.12)";
  ctx.lineWidth = 1;
  const step = 20;
  for (let x = 0; x <= w; x += step) {
    ctx.beginPath();
    ctx.moveTo(x + 0.5, 0);
    ctx.lineTo(x + 0.5, h);
    ctx.stroke();
  }
  for (let y = 0; y <= h; y += step) {
    ctx.beginPath();
    ctx.moveTo(0, y + 0.5);
    ctx.lineTo(w, y + 0.5);
    ctx.stroke();
  }
}

function drawTrace(ctx, samples, w, h) {
  const midY = h / 2;
  const scaleY = h * 0.36;
  ctx.strokeStyle = "#39ff8f";
  ctx.lineWidth = 2;
  ctx.lineJoin = "round";
  ctx.shadowColor = "rgba(57, 255, 143, 0.55)";
  ctx.shadowBlur = 4;
  ctx.beginPath();
  const n = samples.length;
  for (let i = 0; i < n; i++) {
    const x = (i / (n - 1)) * w;
    const y = midY - samples[i] * scaleY;
    if (i === 0) ctx.moveTo(x, y);
    else ctx.lineTo(x, y);
  }
  ctx.stroke();
  ctx.shadowBlur = 0;
}

function renderChoices(q) {
  const { order, correctIndex } = buildChoiceOrder(q);
  const container = document.getElementById("softkeys");
  container.innerHTML = "";

  order.forEach((origIdx, displayIdx) => {
    const btn = document.createElement("button");
    btn.className = "softkey";
    btn.innerHTML = `<span class="softkey-letter">${String.fromCharCode(65 + displayIdx)}</span><span class="softkey-text">${q.choices[origIdx]}</span>`;
    btn.addEventListener("click", () => selectAnswer(q, displayIdx, correctIndex, container));
    container.appendChild(btn);
  });
}

function selectAnswer(q, displayIdx, correctIndex, container) {
  if (state.answered[state.current]) return; // already answered

  const isCorrect = displayIdx === correctIndex;
  state.answered[state.current] = { chosen: displayIdx, correct: isCorrect };
  if (isCorrect) state.score++;

  const btns = container.querySelectorAll(".softkey");
  btns.forEach((btn, i) => {
    btn.classList.add("disabled");
    if (i === correctIndex) btn.classList.add("correct");
    else if (i === displayIdx) btn.classList.add("incorrect");
  });

  document.getElementById("score-live").textContent = state.score;
  document.getElementById("answered-live").textContent = state.current + 1;

  const rationale = document.getElementById("rationale");
  rationale.hidden = false;
  rationale.innerHTML = `<strong>${isCorrect ? "Correct." : "Not quite."}</strong> ${q.rationale}`;
  rationale.className = `rationale ${isCorrect ? "is-correct" : "is-incorrect"}`;

  document.getElementById("next-btn").hidden = false;
  document.getElementById("next-btn").textContent =
    state.current === EKG_QUESTIONS.length - 1 ? "See results →" : "Next question →";
}

function nextQuestion() {
  if (state.current === EKG_QUESTIONS.length - 1) {
    showResults();
    return;
  }
  state.current++;
  renderQuestion();
}

function showResults() {
  document.getElementById("test-shell").hidden = true;
  const screen = document.getElementById("results-screen");
  screen.hidden = false;

  const total = EKG_QUESTIONS.length;
  const pct = Math.round((state.score / total) * 100);
  document.getElementById("final-score").textContent = `${state.score} / ${total}`;
  document.getElementById("final-pct").textContent = `${pct}%`;

  const best = Number(localStorage.getItem(BEST_SCORE_KEY) || 0);
  if (state.score > best) localStorage.setItem(BEST_SCORE_KEY, String(state.score));
  document.getElementById("best-score").textContent = `${Math.max(best, state.score)} / ${total}`;

  const verdict = document.getElementById("final-verdict");
  if (pct >= 90) verdict.textContent = "Excellent — code-ready recognition.";
  else if (pct >= 75) verdict.textContent = "Solid grasp — review the missed items below.";
  else if (pct >= 60) verdict.textContent = "Getting there — focus your review on the categories below.";
  else verdict.textContent = "Keep studying — rhythm recognition takes repetition.";

  const byCat = {};
  EKG_QUESTIONS.forEach((q, i) => {
    const a = state.answered[i];
    byCat[q.category] = byCat[q.category] || { correct: 0, total: 0 };
    byCat[q.category].total++;
    if (a && a.correct) byCat[q.category].correct++;
  });

  const breakdown = document.getElementById("category-breakdown");
  breakdown.innerHTML = "";
  Object.keys(CATEGORY_LABEL).forEach((cat) => {
    if (!byCat[cat]) return;
    const { correct, total: catTotal } = byCat[cat];
    const row = document.createElement("div");
    row.className = "breakdown-row";
    row.innerHTML = `
      <span class="breakdown-label">${CATEGORY_LABEL[cat]}</span>
      <div class="breakdown-track"><div class="breakdown-fill" style="width:${(correct / catTotal) * 100}%"></div></div>
      <span class="breakdown-score">${correct}/${catTotal}</span>
    `;
    breakdown.appendChild(row);
  });

  const missed = document.getElementById("missed-list");
  missed.innerHTML = "";
  let anyMissed = false;
  EKG_QUESTIONS.forEach((q, i) => {
    const a = state.answered[i];
    if (a && !a.correct) {
      anyMissed = true;
      const item = document.createElement("div");
      item.className = "missed-item";
      item.innerHTML = `
        <span class="missed-cat cat-${q.category}">${CATEGORY_LABEL[q.category]}</span>
        <p class="missed-stem">${q.stem}</p>
        <p class="missed-answer">Correct answer: <strong>${q.choices[q.answer]}</strong></p>
        <p class="missed-rationale">${q.rationale}</p>
      `;
      missed.appendChild(item);
    }
  });
  document.getElementById("missed-heading").hidden = !anyMissed;
}

init();
