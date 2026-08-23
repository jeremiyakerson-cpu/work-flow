const BEST_SCORE_KEY_PREFIX = "ekg-zoll-trainer-best-score-";
const SESSION_KEY = "ekg-zoll-trainer-session-v1";
const QUESTIONS_PER_RUN = 25; // sampled from the full bank each attempt
const SIMULATION_SECONDS = 25 * 60; // 25 minutes for 25 questions

const CATEGORY_LABEL = {
  rhythm: "RHYTHM ID",
  meds: "MEDICATIONS",
  condition: "CHANGE IN CONDITION",
  code: "CODE RHYTHM",
};

const MODE_LABEL = {
  practice: "PRACTICE MODE",
  simulation: "SIMULATION TEST — TIMED",
};

const state = {
  mode: "practice",
  order: [], // question indices in play order (shuffled per attempt)
  current: 0,
  answered: [], // per-question: { chosen, correct, timedOut } or null
  score: 0,
  remainingSeconds: SIMULATION_SECONDS,
  timerId: null,
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

// ---------- session persistence (allows resuming a run after a reload) ----------

function saveSession() {
  localStorage.setItem(
    SESSION_KEY,
    JSON.stringify({
      mode: state.mode,
      order: state.order,
      current: state.current,
      answered: state.answered,
      score: state.score,
      remainingSeconds: state.remainingSeconds,
    })
  );
}

function loadSession() {
  try {
    const raw = JSON.parse(localStorage.getItem(SESSION_KEY));
    if (!raw || !Array.isArray(raw.order) || raw.order.length !== QUESTIONS_PER_RUN) return null;
    if (raw.current >= QUESTIONS_PER_RUN) return null; // already finished
    if (raw.order.some((idx) => idx >= EKG_QUESTIONS.length)) return null; // stale bank
    return raw;
  } catch {
    return null;
  }
}

function clearSession() {
  localStorage.removeItem(SESSION_KEY);
}

function init() {
  document.getElementById("mode-practice").addEventListener("click", () => startTest("practice"));
  document.getElementById("mode-simulation").addEventListener("click", () => startTest("simulation"));
  document.getElementById("resume-btn").addEventListener("click", resumeSession);
  document.getElementById("discard-btn").addEventListener("click", discardSession);
  document.getElementById("retry-btn").addEventListener("click", backToStart);
  document.getElementById("next-btn").addEventListener("click", nextQuestion);

  offerResume();
  tickClock();
  setInterval(tickClock, 1000);
}

function offerResume() {
  const saved = loadSession();
  if (!saved) return;
  document.getElementById("resume-banner").hidden = false;
  document.getElementById("mode-select").hidden = true;
  document.getElementById("resume-q-num").textContent = saved.current + 1;
  document.getElementById("resume-mode-label").textContent = MODE_LABEL[saved.mode] || saved.mode;
}

function resumeSession() {
  const saved = loadSession();
  if (!saved) return;
  state.mode = saved.mode;
  state.order = saved.order;
  state.current = saved.current;
  state.answered = saved.answered;
  state.score = saved.score;
  state.remainingSeconds = saved.remainingSeconds;
  enterTestShell();
}

function discardSession() {
  clearSession();
  document.getElementById("resume-banner").hidden = true;
  document.getElementById("mode-select").hidden = false;
}

function tickClock() {
  const el = document.getElementById("monitor-clock");
  if (!el) return;
  const now = new Date();
  el.textContent = now.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" });
}

function startTest(mode) {
  state.mode = mode;
  state.order = shuffle(EKG_QUESTIONS.map((_, i) => i)).slice(0, QUESTIONS_PER_RUN);
  state.current = 0;
  state.answered = new Array(QUESTIONS_PER_RUN).fill(null);
  state.score = 0;
  state.remainingSeconds = SIMULATION_SECONDS;
  saveSession();
  enterTestShell();
}

function enterTestShell() {
  document.getElementById("start-screen").hidden = true;
  document.getElementById("results-screen").hidden = true;
  document.getElementById("test-shell").hidden = false;

  const tag = document.getElementById("mode-tag");
  tag.textContent = MODE_LABEL[state.mode];
  tag.className = `mode-tag mode-${state.mode}`;

  document.getElementById("test-score").hidden = state.mode === "simulation";

  const timerEl = document.getElementById("monitor-timer");
  if (state.mode === "simulation") {
    timerEl.hidden = false;
    startTimer();
  } else {
    timerEl.hidden = true;
    stopTimer();
  }

  renderQuestion();
}

function backToStart() {
  stopTimer();
  document.getElementById("results-screen").hidden = true;
  document.getElementById("start-screen").hidden = false;
  document.getElementById("mode-select").hidden = false;
  document.getElementById("resume-banner").hidden = true;
}

function startTimer() {
  stopTimer();
  renderTimer();
  state.timerId = setInterval(() => {
    state.remainingSeconds--;
    if (state.remainingSeconds <= 0) {
      state.remainingSeconds = 0;
      renderTimer();
      stopTimer();
      finishDueToTimeout();
      return;
    }
    renderTimer();
    saveSession();
  }, 1000);
}

function stopTimer() {
  if (state.timerId) {
    clearInterval(state.timerId);
    state.timerId = null;
  }
}

function renderTimer() {
  const el = document.getElementById("monitor-timer");
  const m = Math.floor(state.remainingSeconds / 60);
  const s = state.remainingSeconds % 60;
  el.textContent = `TIME LEFT ${m}:${String(s).padStart(2, "0")}`;
  el.classList.toggle("timer-critical", state.remainingSeconds <= 60);
}

function finishDueToTimeout() {
  for (let i = 0; i < state.order.length; i++) {
    if (!state.answered[i]) state.answered[i] = { chosen: null, correct: false, timedOut: true };
  }
  showResults();
}

function currentQuestion() {
  return EKG_QUESTIONS[state.order[state.current]];
}

function renderQuestion() {
  const q = currentQuestion();
  const qNum = state.current + 1;

  document.getElementById("q-num").textContent = qNum;
  document.getElementById("test-progress-fill").style.width = `${((qNum - 1) / state.order.length) * 100}%`;
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

  if (!window.__mainMonitor) window.__mainMonitor = EkgMonitor.attach("ekg-canvas");
  const alarmed = !alarm.hidden;
  window.__mainMonitor.setRhythm(q.rhythm, {
    hr: v.hr,
    // pleth runs only when the question's patient is actually perfusing
    perfusing: !!(v.hr && v.hr > 0 && v.nibp && v.nibp !== "--/--"),
    lethal: alarmed,
  });
}

function renderChoices(q) {
  const { order, correctIndex } = buildChoiceOrder(q);
  const container = document.getElementById("softkeys");
  container.innerHTML = "";

  const prior = state.answered[state.current];

  order.forEach((origIdx, displayIdx) => {
    const btn = document.createElement("button");
    btn.className = "softkey";
    btn.innerHTML = `<span class="softkey-letter">${String.fromCharCode(65 + displayIdx)}</span><span class="softkey-text">${q.choices[origIdx]}</span>`;
    btn.addEventListener("click", () => selectAnswer(q, displayIdx, correctIndex, container));
    container.appendChild(btn);
  });

  if (prior) {
    // Already answered (e.g. resumed mid-session) — lock the choices, no re-grading.
    const btns = container.querySelectorAll(".softkey");
    btns.forEach((btn) => btn.classList.add("disabled"));
    document.getElementById("next-btn").hidden = false;
  }
}

function selectAnswer(q, displayIdx, correctIndex, container) {
  if (state.answered[state.current]) return; // already answered

  const isCorrect = displayIdx === correctIndex;
  state.answered[state.current] = { chosen: displayIdx, correct: isCorrect };
  if (isCorrect) state.score++;
  saveSession();
  if (window.EkgStats) {
    EkgStats.record({ kind: "quiz", category: q.category, rhythm: q.rhythm, focus: null, correct: isCorrect });
  }

  const btns = container.querySelectorAll(".softkey");
  btns.forEach((btn, i) => {
    btn.classList.add("disabled");
    if (state.mode === "practice") {
      if (i === correctIndex) btn.classList.add("correct");
      else if (i === displayIdx) btn.classList.add("incorrect");
    } else if (i === displayIdx) {
      btn.classList.add("selected");
    }
  });

  document.getElementById("score-live").textContent = state.score;
  document.getElementById("answered-live").textContent = state.current + 1;

  if (state.mode === "practice") {
    const rationale = document.getElementById("rationale");
    rationale.hidden = false;
    rationale.innerHTML = `<strong>${isCorrect ? "Correct." : "Not quite."}</strong> ${q.rationale}`;
    rationale.className = `rationale ${isCorrect ? "is-correct" : "is-incorrect"}`;
  }

  document.getElementById("next-btn").hidden = false;
  document.getElementById("next-btn").textContent =
    state.current === state.order.length - 1 ? "See results →" : "Next question →";
}

function nextQuestion() {
  if (state.current === state.order.length - 1) {
    stopTimer();
    showResults();
    return;
  }
  state.current++;
  saveSession();
  renderQuestion();
}

function showResults() {
  clearSession();
  document.getElementById("test-shell").hidden = true;
  const screen = document.getElementById("results-screen");
  screen.hidden = false;

  const total = state.order.length;
  const pct = Math.round((state.score / total) * 100);
  document.getElementById("final-score").textContent = `${state.score} / ${total}`;
  document.getElementById("final-pct").textContent = `${pct}%`;

  const bestKey = BEST_SCORE_KEY_PREFIX + state.mode;
  const best = Number(localStorage.getItem(bestKey) || 0);
  if (state.score > best) localStorage.setItem(bestKey, String(state.score));
  document.getElementById("best-score").textContent = `${Math.max(best, state.score)} / ${total}`;
  document.getElementById("best-score-label").textContent = `BEST — ${MODE_LABEL[state.mode]}`;

  const verdict = document.getElementById("final-verdict");
  const timedOutCount = state.answered.filter((a) => a && a.timedOut).length;
  let verdictText;
  if (pct >= 90) verdictText = "Excellent — code-ready recognition.";
  else if (pct >= 75) verdictText = "Solid grasp — review the missed items below.";
  else if (pct >= 60) verdictText = "Getting there — focus your review on the categories below.";
  else verdictText = "Keep studying — rhythm recognition takes repetition.";
  if (timedOutCount > 0) {
    verdictText += ` Time expired with ${timedOutCount} question${timedOutCount === 1 ? "" : "s"} unanswered — those are scored as incorrect.`;
  }
  verdict.textContent = verdictText;

  const byCat = {};
  state.order.forEach((qIdx, i) => {
    const q = EKG_QUESTIONS[qIdx];
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
  state.order.forEach((qIdx, i) => {
    const q = EKG_QUESTIONS[qIdx];
    const a = state.answered[i];
    if (a && !a.correct) {
      anyMissed = true;
      const item = document.createElement("div");
      item.className = "missed-item";
      item.innerHTML = `
        <span class="missed-cat cat-${q.category}">${CATEGORY_LABEL[q.category]}</span>
        ${a.timedOut ? '<span class="missed-timeout">NOT ANSWERED — TIME EXPIRED</span>' : ""}
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
