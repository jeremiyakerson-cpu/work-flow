/*
 * Code Log: a persistent index of every scenario run — completed,
 * patient lost, or exited early — with per-case pointers built from the
 * decisions that were missed, and pattern-level coaching across runs.
 * All data stays on-device.
 */

(function () {
  const LOG_KEY = "ekg-code-log-v1";
  const LOG_CAP = 50;

  const OUTCOME_META = {
    completed: { label: "COMPLETED", cls: "log-completed" },
    died: { label: "PATIENT LOST", cls: "log-died" },
    abandoned: { label: "EXITED EARLY", cls: "log-abandoned" },
  };

  const FOCUS_LABEL = {
    "shockable-arrest": "shockable-arrest (VF/pVT)",
    "nonshockable-arrest": "PEA/asystole",
    bradycardia: "bradycardia",
    tachycardia: "tachycardia",
    "torsades-qt": "torsades / prolonged-QT",
    "stable-vt-acs": "VT & ACS",
  };

  const FOCUS_HINT = {
    "shockable-arrest": "review the VF/pVT algorithm — shock-first sequencing, epi after the 2nd shock, amiodarone 300 mg for refractory rhythms.",
    "nonshockable-arrest": "review PEA/asystole care — never shock, epinephrine early, and drill the H's & T's until the cause-hunt is reflexive.",
    bradycardia: "review the bradycardia ladder — atropine 1 mg (max 3), pacing when it fails, dopamine/epi infusions, and capture checks.",
    tachycardia: "review tachycardia care — vagal → adenosine for SVT, rate control for afib, and the moment instability switches you to synchronized cardioversion.",
    "torsades-qt": "review torsades — magnesium first, defibrillate if pulseless, stop QT-prolonging drugs, and remember amiodarone makes it worse.",
    "stable-vt-acs": "review VT with a pulse — amiodarone 150 mg over 10 min while stable, immediate defibrillation the moment the pulse is lost, and reperfusion for the underlying STEMI.",
  };

  function load() {
    try {
      const raw = JSON.parse(localStorage.getItem(LOG_KEY));
      if (Array.isArray(raw)) return raw;
    } catch {}
    return [];
  }

  function save(list) {
    try {
      localStorage.setItem(LOG_KEY, JSON.stringify(list.slice(0, LOG_CAP)));
    } catch {}
  }

  const DIFFICULTY_LABEL = { guided: "GUIDED", standard: "STANDARD", expert: "EXPERT" };

  /*
   * entry: { title, scenarioId, generated, focus, outcome, correct,
   *          answered, total, durationMs, misses:[{question, intervention,
   *          rationale, critical, rhythm?, timedOut?}], t,
   *          difficulty?, metrics?: { timeToFirstShockMs, timeToEpiMs,
   *          timeToCardioversionMs, lateDecisions, timeouts, branches,
   *          recovered, meanDecisionMs },
   *          timeline?: [{ t, stage, attempt, text, rhythm, correct, late,
   *          timedOut, critical, decisionMs, branch, next }] }
   * (difficulty/metrics/timeline/rhythm are absent on older entries.)
   */
  function add(entry) {
    const list = load();
    list.unshift({ ...entry, t: Date.now() });
    save(list);
  }

  function list() {
    return load();
  }

  function count() {
    return load().length;
  }

  // First sentence of a rationale = the pointer.
  function firstSentence(text) {
    const m = /^(.*?[.!?])(\s|$)/.exec(text || "");
    return m ? m[1] : text || "";
  }

  function pointersFor(entry) {
    return (entry.misses || []).map((miss) => ({
      critical: miss.critical,
      text: `${miss.question} → ${firstSentence(miss.rationale)}`,
    }));
  }

  /*
   * Per-rhythm decision record across recent logged runs — the one
   * learner-data read the scenario generator uses to target weak rhythms.
   * Returns { [rhythmKey]: { seen, missed, rate } } counting first attempts
   * at each decision (timeline entries with attempt 0). Older entries
   * without a timeline contribute only their misses that carry a rhythm.
   */
  const WEAKNESS_WINDOW = 30; // most recent runs considered

  function rhythmWeakness() {
    const out = {};
    const bump = (r, missed) => {
      if (!r) return;
      const b = out[r] || (out[r] = { seen: 0, missed: 0, rate: 0 });
      b.seen++;
      if (missed) b.missed++;
    };
    for (const e of load().slice(0, WEAKNESS_WINDOW)) {
      if (Array.isArray(e.timeline)) {
        e.timeline.filter((x) => x && x.attempt === 0).forEach((x) => bump(x.rhythm, !x.correct));
      } else {
        (e.misses || []).forEach((m) => bump(m.rhythm, true));
      }
    }
    for (const r of Object.keys(out)) out[r].rate = out[r].missed / out[r].seen;
    return out;
  }

  function focusHint(focus) {
    return FOCUS_HINT[focus] || null;
  }

  // Cross-run coaching: recurring weak focus areas, deaths, and streaks.
  function patterns() {
    const entries = load();
    if (!entries.length) return [];
    const out = [];

    const deaths = entries.filter((e) => e.outcome === "died");
    if (deaths.length) {
      const byFocus = {};
      deaths.forEach((e) => (byFocus[e.focus] = (byFocus[e.focus] || 0) + 1));
      const worst = Object.keys(byFocus).sort((a, b) => byFocus[b] - byFocus[a])[0];
      out.push(
        `☠ ${deaths.length} patient${deaths.length === 1 ? "" : "s"} lost across ${entries.length} logged runs` +
          (worst && FOCUS_LABEL[worst] ? ` — most often in ${FOCUS_LABEL[worst]} cases.` : ".")
      );
    }

    const missByFocus = {};
    entries.forEach((e) => {
      const n = (e.misses || []).length;
      if (n && e.focus) missByFocus[e.focus] = (missByFocus[e.focus] || 0) + n;
    });
    const ranked = Object.keys(missByFocus).sort((a, b) => missByFocus[b] - missByFocus[a]);
    if (ranked.length) {
      const top = ranked[0];
      out.push(
        `⚠ Most missed decisions: ${FOCUS_LABEL[top] || top} (${missByFocus[top]}) — ${FOCUS_HINT[top] || "revisit this area in the study guide."}`
      );
      if (ranked[1]) {
        out.push(`△ Next: ${FOCUS_LABEL[ranked[1]] || ranked[1]} (${missByFocus[ranked[1]]}) — ${FOCUS_HINT[ranked[1]] || ""}`);
      }
    }

    const totalMisses = entries.reduce((n, e) => n + (e.misses || []).length, 0);
    if (totalMisses === 0) {
      out.push("★ No missed decisions in any logged run — generate harder cases and keep the streak alive.");
    }

    const recent = entries.slice(0, 5);
    if (recent.length >= 3 && recent.every((e) => e.outcome === "completed" && (e.misses || []).length === 0)) {
      out.push(`★ Last ${recent.length} runs flawless — the generator will keep rotating fresh cases at you.`);
    }

    return out;
  }

  function metricsLine(e) {
    const m = e.metrics;
    if (!m) return "";
    const bits = [];
    if (m.timeToFirstShockMs != null) bits.push(`1st shock ${fmtDuration(m.timeToFirstShockMs)}`);
    if (m.timeToEpiMs != null) bits.push(`epi ${fmtDuration(m.timeToEpiMs)}`);
    if (m.timeToCardioversionMs != null) bits.push(`cardioversion ${fmtDuration(m.timeToCardioversionMs)}`);
    if (m.lateDecisions) bits.push(`${m.lateDecisions} late`);
    if (m.timeouts) bits.push(`${m.timeouts} timed out`);
    if (m.branches) bits.push(`${m.branches} branch${m.branches === 1 ? "" : "es"}`);
    return bits.length ? `<div class="codelog-metrics">${bits.join(" · ")}</div>` : "";
  }

  function fmtDuration(ms) {
    if (!ms && ms !== 0) return "";
    const s = Math.floor(ms / 1000);
    return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`;
  }

  // ---------- screen ----------

  function open() {
    window.showScreen("codelog-screen");
    const entries = load();

    const patEl = document.getElementById("codelog-patterns");
    const pats = patterns();
    patEl.innerHTML = pats.length
      ? pats.map((p) => `<div class="codelog-pattern">${p}</div>`).join("")
      : `<div class="codelog-pattern codelog-pattern-empty">Run some scenarios — every finished, abandoned, or fatal code lands here with pointers on what to review.</div>`;

    const listEl = document.getElementById("codelog-list");
    if (!entries.length) {
      listEl.innerHTML = "";
      return;
    }
    listEl.innerHTML = entries
      .map((e) => {
        const meta = OUTCOME_META[e.outcome] || OUTCOME_META.abandoned;
        const d = new Date(e.t);
        const pts = pointersFor(e);
        const ptsHtml = pts.length
          ? `<details class="codelog-details">
               <summary>Pointers (${pts.length})</summary>
               ${pts.map((p) => `<div class="codelog-pointer ${p.critical ? "pointer-critical" : ""}">${p.critical ? "⛔" : "•"} ${p.text}</div>`).join("")}
             </details>`
          : `<div class="codelog-clean">Clean run — no missed decisions.</div>`;
        return `
        <div class="codelog-entry ${meta.cls}">
          <div class="codelog-entry-head">
            <span class="codelog-badge">${meta.label}</span>
            ${DIFFICULTY_LABEL[e.difficulty] ? `<span class="codelog-diff">${DIFFICULTY_LABEL[e.difficulty]}</span>` : ""}
            <span class="codelog-title">${e.title}</span>
            <span class="codelog-meta">${e.correct}/${e.answered} decisions · ${fmtDuration(e.durationMs)} · ${d.toLocaleDateString()} ${d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</span>
          </div>
          ${metricsLine(e)}
          ${ptsHtml}
        </div>`;
      })
      .join("");
  }

  function init() {
    document.getElementById("sc-open-log").addEventListener("click", open);
    document.getElementById("codelog-back").addEventListener("click", () => scOpenPicker());
    document.getElementById("sc-debrief-log-btn").addEventListener("click", open);
    const clear = document.getElementById("codelog-clear");
    clear.addEventListener("click", () => {
      if (!confirm("Clear the entire code log? This cannot be undone.")) return;
      save([]);
      open();
    });
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
  else init();

  window.EkgCodeLog = { add, list, count, patterns, pointersFor, rhythmWeakness, focusHint, fmtDuration, open };
})();
