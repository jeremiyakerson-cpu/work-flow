/*
 * Progress dashboard: headline stats, category accuracy bars, a
 * per-rhythm mastery grid, exam history, and scenario completion.
 * All data comes from on-device tracking (EkgStats + localStorage).
 */

(function () {
  const EXAM_HISTORY_KEY = "ekg-exam-history-v1";
  const CATEGORY_LABELS = { rhythm: "Rhythm ID", meds: "Medications", condition: "Change in condition", code: "Code management" };

  function examHistory() {
    try {
      const raw = JSON.parse(localStorage.getItem(EXAM_HISTORY_KEY));
      if (Array.isArray(raw)) return raw;
    } catch {}
    return [];
  }

  function open() {
    window.showScreen("progress-screen");
    const p = EkgStats.profile();
    const history = examHistory();
    const passes = history.filter((h) => h.pass).length;

    // scenario completion (authored + generated bests)
    let scenariosDone = 0;
    EKG_SCENARIOS.forEach((s) => {
      if (localStorage.getItem("ekg-zoll-scenario-best-" + s.id) !== null) scenariosDone++;
    });

    document.getElementById("prog-tiles").innerHTML = `
      <div class="stat-cell"><div class="stat-value">${p.total}</div><div class="stat-label">ANSWERS TRACKED</div></div>
      <div class="stat-cell"><div class="stat-value">${p.total ? Math.round((p.correct / p.total) * 100) + "%" : "--"}</div><div class="stat-label">OVERALL ACCURACY</div></div>
      <div class="stat-cell"><div class="stat-value">${passes} / ${history.length}</div><div class="stat-label">EXAMS PASSED</div></div>
      <div class="stat-cell"><div class="stat-value">${scenariosDone} / ${EKG_SCENARIOS.length}</div><div class="stat-label">AUTHORED SCENARIOS RUN</div></div>
    `;

    // category bars
    const cats = document.getElementById("prog-categories");
    cats.innerHTML = "";
    for (const key of Object.keys(CATEGORY_LABELS)) {
      const b = p.byCategory[key];
      const pct = b && b.total ? Math.round(b.accuracy * 100) : null;
      const row = document.createElement("div");
      row.className = "breakdown-row";
      row.innerHTML = `
        <span class="breakdown-label">${CATEGORY_LABELS[key]}</span>
        <div class="breakdown-track"><div class="breakdown-fill" style="width:${pct ?? 0}%"></div></div>
        <span class="breakdown-score">${pct !== null ? pct + "%" : "—"}</span>
      `;
      cats.appendChild(row);
    }

    // rhythm mastery grid
    const grid = document.getElementById("prog-rhythms");
    grid.innerHTML = "";
    for (const key of Object.keys(EkgEducation.RHYTHM_GUIDE)) {
      const b = p.byRhythm[key];
      let cls = "none";
      let label = "—";
      if (b && b.total > 0) {
        const pct = Math.round(b.accuracy * 100);
        label = `${pct}%`;
        cls = pct >= 80 ? "good" : pct >= 50 ? "warn" : "bad";
      }
      const chip = document.createElement("div");
      chip.className = `mastery-chip mastery-${cls}`;
      chip.innerHTML = `<span class="mastery-name">${EkgEducation.RHYTHM_GUIDE[key].name}</span><span class="mastery-pct">${label}</span>`;
      grid.appendChild(chip);
    }

    // exam history
    const hist = document.getElementById("prog-exams");
    hist.innerHTML = history.length
      ? history
          .map((h) => {
            const d = new Date(h.t);
            return `<div class="exam-history-row ${h.pass ? "passed" : "failed"}">
              <span>${d.toLocaleDateString()} ${d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</span>
              <span>${h.score}/${h.total} (${h.pct}%)</span>
              <span class="exam-history-badge">${h.pass ? "PASS" : "FAIL"}</span>
            </div>`;
          })
          .join("")
      : `<p class="prog-empty">No comprehensive exams taken yet.</p>`;
  }

  function resetAll() {
    if (!confirm("Reset ALL tracked progress? This clears your stats, exam history, sprint best, and scenario best scores (generated cases stay).")) return;
    EkgStats.reset();
    localStorage.removeItem(EXAM_HISTORY_KEY);
    localStorage.removeItem("ekg-sprint-best-v1");
    Object.keys(localStorage)
      .filter((k) => k.startsWith("ekg-zoll-scenario-best-") || k.startsWith("ekg-zoll-trainer-best-score-"))
      .forEach((k) => localStorage.removeItem(k));
    open();
  }

  function init() {
    document.getElementById("mode-progress").addEventListener("click", open);
    document.getElementById("progress-back").addEventListener("click", () => window.showScreen("start-screen"));
    document.getElementById("progress-reset").addEventListener("click", resetAll);
  }

  init();
  window.EkgProgress = { open, examHistory, EXAM_HISTORY_KEY };
})();
