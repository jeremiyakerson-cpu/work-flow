/*
 * Rhythm Sprint: rapid-fire strip recognition. 20 random live strips,
 * a running stopwatch, smart distractors drawn from confusable rhythm
 * groups, immediate feedback with the recognition criteria, and a
 * per-rhythm miss review. Answers feed the performance profile.
 */

(function () {
  const ROUNDS = 20;
  const BEST_KEY = "ekg-sprint-best-v1";
  const { RHYTHM_GUIDE, SPRINT_CONFUSION } = EkgEducation;
  const ALL_KEYS = Object.keys(RHYTHM_GUIDE);

  const st = {
    round: 0,
    correct: 0,
    startTime: 0,
    timerId: null,
    advanceId: null, // pending "next strip" delay after an answer
    current: null,
    misses: [],
    monitor: null,
    lastKey: null,
    shownAt: 0,
  };

  function loadBest() {
    try {
      const best = JSON.parse(localStorage.getItem(BEST_KEY));
      if (best && Number.isFinite(best.correct) && Number.isFinite(best.ms)) return best;
    } catch {}
    return null;
  }

  // Cancel the clock and any pending strip advance — called whenever the
  // sprint is left or restarted, so a stale timeout can't skip a strip or
  // finish a run the user already walked away from.
  function stopTimers() {
    clearInterval(st.timerId);
    clearTimeout(st.advanceId);
    st.timerId = null;
    st.advanceId = null;
    delete document.getElementById("sprint-choices").dataset.locked;
  }

  const shuffle = (arr) => {
    const a = arr.slice();
    for (let i = a.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [a[i], a[j]] = [a[j], a[i]];
    }
    return a;
  };

  function fmtTime(ms) {
    const s = Math.floor(ms / 1000);
    return `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;
  }

  function open() {
    window.showScreen("sprint-screen");
    document.getElementById("sprint-intro").hidden = false;
    document.getElementById("sprint-run").hidden = true;
    document.getElementById("sprint-results").hidden = true;
    const best = loadBest();
    document.getElementById("sprint-best").textContent = best
      ? `Personal best: ${best.correct}/${ROUNDS} in ${fmtTime(best.ms)}`
      : "No sprint completed yet.";
  }

  function start() {
    stopTimers();
    st.round = 0;
    st.correct = 0;
    st.misses = [];
    st.startTime = Date.now();
    document.getElementById("sprint-intro").hidden = true;
    document.getElementById("sprint-results").hidden = true;
    document.getElementById("sprint-run").hidden = false;
    if (!st.monitor) st.monitor = EkgMonitor.attach("sprint-canvas");
    document.getElementById("sprint-clock").textContent = "00:00";
    st.timerId = setInterval(() => {
      document.getElementById("sprint-clock").textContent = fmtTime(Date.now() - st.startTime);
    }, 250);
    nextRound();
  }

  function pickRhythm() {
    let key;
    do {
      key = ALL_KEYS[Math.floor(Math.random() * ALL_KEYS.length)];
    } while (key === st.lastKey);
    st.lastKey = key;
    return key;
  }

  function buildChoices(answerKey) {
    const group = SPRINT_CONFUSION.find((g) => g.includes(answerKey)) || [];
    const inGroup = shuffle(group.filter((k) => k !== answerKey)).slice(0, 2);
    const outside = shuffle(ALL_KEYS.filter((k) => k !== answerKey && !inGroup.includes(k)));
    const distractors = [...inGroup, ...outside].slice(0, 3);
    return shuffle([answerKey, ...distractors]);
  }

  function nextRound() {
    if (st.round >= ROUNDS) {
      finish();
      return;
    }
    st.round++;
    const key = pickRhythm();
    st.current = key;
    const g = RHYTHM_GUIDE[key];
    document.getElementById("sprint-progress").textContent = `Strip ${st.round} / ${ROUNDS}`;
    document.getElementById("sprint-score").textContent = `${st.correct} correct`;
    st.monitor.setRhythm(key, { hr: g.demo.hr, perfusing: g.demo.perfusing, lethal: false });

    const container = document.getElementById("sprint-choices");
    container.innerHTML = "";
    const feedback = document.getElementById("sprint-feedback");
    feedback.hidden = true;

    buildChoices(key).forEach((choiceKey, i) => {
      const btn = document.createElement("button");
      btn.className = "softkey";
      btn.innerHTML = `<span class="softkey-letter">${String.fromCharCode(65 + i)}</span><span class="softkey-text">${RHYTHM_GUIDE[choiceKey].name}</span>`;
      btn.addEventListener("click", () => answer(choiceKey, btn, container));
      container.appendChild(btn);
    });
    st.shownAt = Date.now();
  }

  function answer(choiceKey, btn, container) {
    if (container.dataset.locked) return;
    container.dataset.locked = "1";
    const correct = choiceKey === st.current;
    if (correct) st.correct++;
    else st.misses.push(st.current);
    if (window.EkgStats) {
      EkgStats.record({
        kind: "sprint",
        category: "rhythm",
        rhythm: st.current,
        focus: null,
        correct,
        item: "s:" + st.current,
        ms: Date.now() - st.shownAt,
        chosen: choiceKey,
      });
    }

    container.querySelectorAll(".softkey").forEach((b) => {
      b.classList.add("disabled");
      b.setAttribute("aria-disabled", "true");
      const txt = b.querySelector(".softkey-text").textContent;
      if (txt === RHYTHM_GUIDE[st.current].name) b.classList.add("correct");
      else if (b === btn) b.classList.add("incorrect");
    });

    const g = RHYTHM_GUIDE[st.current];
    const feedback = document.getElementById("sprint-feedback");
    feedback.hidden = false;
    feedback.className = `rationale ${correct ? "is-correct" : "is-incorrect"}`;
    feedback.innerHTML = `<strong>${correct ? "Correct." : g.name + "."}</strong> ${g.criteria.regular} · rate ${g.criteria.rate} · P: ${g.criteria.p} · QRS ${g.criteria.qrs}`;

    // brief pause so the feedback registers, then next strip
    st.advanceId = setTimeout(() => {
      st.advanceId = null;
      delete container.dataset.locked;
      nextRound();
    }, correct ? 900 : 1900);
  }

  function finish() {
    stopTimers();
    const ms = Date.now() - st.startTime;
    document.getElementById("sprint-run").hidden = true;
    const res = document.getElementById("sprint-results");
    res.hidden = false;
    document.getElementById("sprint-final-score").textContent = `${st.correct} / ${ROUNDS}`;
    document.getElementById("sprint-final-time").textContent = fmtTime(ms);

    const best = loadBest();
    const better = !best || st.correct > best.correct || (st.correct === best.correct && ms < best.ms);
    if (better) {
      try {
        localStorage.setItem(BEST_KEY, JSON.stringify({ correct: st.correct, ms }));
      } catch {}
    }
    document.getElementById("sprint-new-best").hidden = !better;

    const missEl = document.getElementById("sprint-misses");
    if (st.misses.length === 0) {
      missEl.innerHTML = `<p class="sprint-perfect">Perfect recognition — every strip identified.</p>`;
    } else {
      const counts = {};
      st.misses.forEach((k) => (counts[k] = (counts[k] || 0) + 1));
      missEl.innerHTML =
        `<h3>Missed strips — study these</h3>` +
        Object.keys(counts)
          .map((k) => {
            const g = RHYTHM_GUIDE[k];
            return `<div class="sprint-miss-item"><strong>${g.name}</strong> ×${counts[k]} — ${g.criteria.regular}, rate ${g.criteria.rate}, P: ${g.criteria.p}</div>`;
          })
          .join("");
    }
  }

  function init() {
    document.getElementById("mode-sprint").addEventListener("click", open);
    document.getElementById("sprint-start").addEventListener("click", start);
    document.getElementById("sprint-again").addEventListener("click", start);
    document.getElementById("sprint-back").addEventListener("click", () => {
      stopTimers();
      window.showScreen("start-screen");
    });
    document.getElementById("sprint-results-back").addEventListener("click", () => window.showScreen("start-screen"));
  }

  init();
  window.EkgSprint = { open };
})();
