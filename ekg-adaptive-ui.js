/*
 * Adaptive practice screen and the home "what to study next" card.
 * The model and scheduler live in ekg-adaptive.js; this file only draws
 * them. A round is a mix of live strips and bank questions chosen by the
 * scheduler; every answer is recorded through EkgStats (which feeds the
 * mastery model), so it also counts toward the progress dashboard.
 */

(function () {
  const A = window.EkgAdaptive;
  const { RHYTHM_GUIDE } = EkgEducation;
  const CATEGORY_LABEL = { rhythm: "RHYTHM ID", meds: "MEDICATIONS", condition: "CHANGE IN CONDITION", code: "CODE RHYTHM" };
  const REASON_LABEL = {
    due: "DUE REVIEW",
    weak: "WEAK SPOT",
    "look-alike": "LOOK-ALIKE DRILL",
    new: "NEW",
    practice: "PRACTICE",
  };
  const LEVEL_CLS = { mastered: "good", learning: "warn", weak: "bad", new: "none" };
  const $ = (id) => document.getElementById(id);

  const st = {
    plan: [],
    i: 0,
    correct: 0,
    shownAt: 0,
    answered: false,
    before: null, // rhythm mastery at round start, for the "what changed" list
    monitor: null,
  };

  const shuffle = (arr) => {
    const a = arr.slice();
    for (let i = a.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [a[i], a[j]] = [a[j], a[i]];
    }
    return a;
  };

  const pct = (x) => `${Math.round(x * 100)}%`;

  // ---------- home card ----------

  function renderHome() {
    const plan = A.dailyPlan();
    $("next-goal-count").textContent = `${Math.min(plan.doneToday, 999)} / ${plan.goal}`;
    $("next-goal-fill").style.width = `${Math.min(100, (plan.doneToday / plan.goal) * 100)}%`;
    $("next-streak").textContent = plan.streak > 1 ? `🔥 ${plan.streak}-day streak` : "";

    $("next-steps").innerHTML = plan.steps.map((s) => `<li class="next-step next-step-${s.kind}">${s.text}</li>`).join("");

    const strip = $("next-mastery");
    strip.innerHTML = plan.rhythms
      .map((r) => `<span class="mastery-seg mastery-seg-${LEVEL_CLS[r.level]}" title="${r.name}: ${r.level}"></span>`)
      .join("");
    const counts = { mastered: 0, learning: 0, weak: 0, new: 0 };
    plan.rhythms.forEach((r) => counts[r.level]++);
    const summary = `${counts.mastered} mastered, ${counts.learning} learning, ${counts.weak} weak, ${counts.new} not started`;
    strip.setAttribute("aria-label", `Rhythm mastery: ${summary}`);
    $("next-mastery-label").textContent = `${plan.mastered} of ${plan.total} rhythms mastered · ${summary.split(", ").slice(1).join(" · ")}`;

    const studyBtn = $("next-study");
    if (plan.studyKey) {
      studyBtn.hidden = false;
      studyBtn.dataset.key = plan.studyKey;
      studyBtn.textContent = `📖 Study ${RHYTHM_GUIDE[plan.studyKey].name}`;
    } else {
      studyBtn.hidden = true;
    }
  }

  // ---------- adaptive round ----------

  function start() {
    st.plan = A.planSession();
    st.i = 0;
    st.correct = 0;
    st.before = new Map(A.rhythmMastery().map((r) => [r.key, r]));
    window.showScreen("adaptive-screen");
    $("ad-run").hidden = false;
    $("ad-results").hidden = true;
    if (!st.monitor) st.monitor = EkgMonitor.attach("ad-canvas");
    render();
  }

  function render() {
    const item = st.plan[st.i];
    st.answered = false;
    $("ad-progress").textContent = `Item ${st.i + 1} / ${st.plan.length}`;
    $("ad-score").textContent = `${st.correct} correct`;
    const reason = $("ad-reason");
    reason.textContent = REASON_LABEL[item.reason] || "";
    reason.className = `ad-reason ad-reason-${item.reason}`;
    $("ad-feedback-wrap").hidden = true;

    const cat = $("ad-category");
    const vitals = $("ad-vitals");
    const container = $("ad-choices");
    container.innerHTML = "";
    let choices;

    if (item.kind === "strip") {
      const g = RHYTHM_GUIDE[item.rhythm];
      cat.textContent = `RHYTHM ID · LEVEL ${item.tier}`;
      cat.className = "q-category cat-rhythm";
      $("ad-stem").textContent =
        item.tier >= 3
          ? "Identify this rhythm — the choices are the look-alikes you find hardest."
          : item.tier === 2
          ? "Identify this rhythm. One choice is a common look-alike."
          : "Identify this rhythm.";
      vitals.hidden = true;
      st.monitor.setRhythm(item.rhythm, { hr: g.demo.hr, perfusing: g.demo.perfusing, lethal: false });
      choices = A.stripChoices(item.rhythm, item.tier).map((k) => ({ label: RHYTHM_GUIDE[k].name, key: k, correct: k === item.rhythm }));
    } else {
      const q = item.q;
      cat.textContent = `${CATEGORY_LABEL[q.category] || q.category.toUpperCase()} · LEVEL ${item.tier}`;
      cat.className = `q-category cat-${q.category}`;
      $("ad-stem").textContent = q.stem;
      const v = q.vitals || {};
      const parts = [];
      if (v.hr !== undefined) parts.push(`HR ${v.hr === null ? "--" : v.hr}`);
      if (v.spo2 !== undefined) parts.push(`SpO₂ ${v.spo2 === null ? "--" : v.spo2 + "%"}`);
      if (v.nibp) parts.push(`NIBP ${v.nibp}`);
      if (v.rr !== undefined) parts.push(`RR ${v.rr === null ? "--" : v.rr}`);
      vitals.textContent = parts.join(" · ");
      vitals.hidden = !parts.length;
      st.monitor.setRhythm(q.rhythm, {
        hr: v.hr,
        perfusing: !!(v.hr && v.hr > 0 && v.nibp && v.nibp !== "--/--"),
        lethal: false,
      });
      choices = shuffle(q.choices.map((label, i) => ({ label, correct: i === q.answer })));
    }

    choices.forEach((c, i) => {
      const btn = document.createElement("button");
      btn.className = "softkey";
      btn.type = "button";
      btn.innerHTML = `<span class="softkey-letter">${String.fromCharCode(65 + i)}</span><span class="softkey-text"></span>`;
      btn.querySelector(".softkey-text").textContent = c.label;
      if (c.correct) btn.dataset.correct = "1";
      btn.addEventListener("click", () => answer(item, c, btn));
      container.appendChild(btn);
    });
    st.shownAt = Date.now();
  }

  function answer(item, choice, btn) {
    if (st.answered) return;
    st.answered = true;
    const ms = Date.now() - st.shownAt;
    const correct = choice.correct;
    if (correct) st.correct++;
    const before = A.rhythmMastery().find((r) => r.key === item.rhythm);

    EkgStats.record({
      kind: "adaptive",
      category: item.topic,
      rhythm: item.rhythm,
      focus: null,
      correct,
      item: item.id,
      ms,
      chosen: item.kind === "strip" ? choice.key : undefined,
    });

    const container = $("ad-choices");
    container.querySelectorAll(".softkey").forEach((b) => {
      b.classList.add("disabled");
      b.setAttribute("aria-disabled", "true");
      if (b.dataset.correct) b.classList.add("correct");
      else if (b === btn) b.classList.add("incorrect");
    });

    const fb = $("ad-feedback");
    fb.className = `rationale ${correct ? "is-correct" : "is-incorrect"}`;
    if (item.kind === "strip") {
      const g = RHYTHM_GUIDE[item.rhythm];
      fb.innerHTML = `<strong>${correct ? "Correct." : "This is " + g.name + "."}</strong> ${g.criteria.regular} · rate ${g.criteria.rate} · P: ${g.criteria.p} · QRS ${g.criteria.qrs}`;
    } else {
      fb.innerHTML = `<strong>${correct ? "Correct." : "Not quite."}</strong> ${item.q.rationale}`;
    }

    if (RHYTHM_GUIDE[item.rhythm]) {
      const after = A.rhythmMastery().find((r) => r.key === item.rhythm);
      const card = A.load().items[item.id];
      const when = card ? describeDue(card.due - Date.now()) : "";
      $("ad-delta").textContent = `${after.name} mastery ${pct(before.mastery)} → ${pct(after.mastery)}${when ? ` · next review ${when}` : ""}`;
    } else {
      $("ad-delta").textContent = "";
    }

    $("ad-next").textContent = st.i === st.plan.length - 1 ? "See results →" : "Next →";
    const wrap = $("ad-feedback-wrap");
    wrap.hidden = false;
    $("ad-next").focus({ preventScroll: true });
    const r = wrap.getBoundingClientRect();
    if (r.bottom > window.innerHeight) wrap.scrollIntoView({ block: "nearest" });
  }

  function describeDue(ms) {
    const min = Math.round(ms / 60000);
    if (min < 60) return `in ${Math.max(1, min)} min`;
    const days = Math.round(ms / 86400000);
    return days <= 1 ? "tomorrow" : `in ${days} days`;
  }

  function next() {
    if (!st.answered) return;
    if (st.i >= st.plan.length - 1) {
      finish();
      return;
    }
    st.i++;
    render();
    window.scrollTo(0, 0);
  }

  function finish() {
    $("ad-run").hidden = true;
    $("ad-results").hidden = false;
    $("ad-final-score").textContent = `${st.correct} / ${st.plan.length}`;
    const now = A.rhythmMastery();
    $("ad-final-mastered").textContent = `${now.filter((r) => r.level === "mastered").length} / ${now.length}`;

    const touched = new Set(st.plan.map((p) => p.rhythm));
    const rows = now
      .filter((r) => touched.has(r.key))
      .map((r) => ({ r, was: st.before.get(r.key) }))
      .sort((a, b) => b.r.mastery - b.was.mastery - (a.r.mastery - a.was.mastery));
    $("ad-changes").innerHTML =
      `<h3>Mastery this round</h3>` +
      rows
        .map(({ r, was }) => {
          const d = Math.round((r.mastery - was.mastery) * 100);
          return `<div class="mastery-chip mastery-${LEVEL_CLS[r.level]}"><span class="mastery-name">${r.name}<small class="mastery-detail">${r.level}</small></span><span class="mastery-pct">${pct(r.mastery)} <small>${d >= 0 ? "+" : ""}${d}</small></span></div>`;
        })
        .join("");

    const plan = A.dailyPlan();
    $("ad-next-due").textContent = plan.dueCount
      ? `${plan.dueCount} item${plan.dueCount === 1 ? " is" : "s are"} already due again — missed items come back within minutes.`
      : "Nothing is due right now. Items you got right come back in a day or more, spaced further each time you remember them.";
  }

  function backToMenu() {
    window.showScreen("start-screen");
  }

  function init() {
    $("mode-adaptive").addEventListener("click", start);
    $("ad-again").addEventListener("click", start);
    $("ad-next").addEventListener("click", next);
    $("adaptive-back").addEventListener("click", backToMenu);
    $("ad-results-back").addEventListener("click", backToMenu);
    $("next-study").addEventListener("click", (e) => window.EkgStudy && EkgStudy.open(e.currentTarget.dataset.key));
    document.addEventListener("ekg:screen", (e) => {
      if (e.detail && e.detail.id === "start-screen") renderHome();
    });
    renderHome();
  }

  init();
  window.EkgAdaptiveUI = { renderHome, start };
})();
