/*
 * Usage tracking for the EKG trainer. Records every answered question
 * (quiz and scenario) into localStorage and aggregates a performance
 * profile: accuracy by category, by rhythm, and by clinical focus area.
 * The scenario generator reads this profile to target weak areas.
 * All data stays on-device.
 */

(function () {
  const STATS_KEY = "ekg-zoll-trainer-stats-v1";
  const MAX_EVENTS = 400; // rolling window of most-recent answers

  function load() {
    try {
      const raw = JSON.parse(localStorage.getItem(STATS_KEY));
      if (raw && Array.isArray(raw.events)) return raw;
    } catch {}
    return { events: [] };
  }

  function save(data) {
    try {
      localStorage.setItem(STATS_KEY, JSON.stringify(data));
    } catch {}
  }

  /*
   * event: {
   *   kind: "quiz" | "scenario",
   *   category: "rhythm"|"meds"|"condition"|"code",
   *   rhythm: rhythm key shown on the monitor,
   *   focus: clinical focus tag (scenario arcs / question topics),
   *   correct: boolean,
   *   t: epoch ms
   * }
   */
  function record(event) {
    const data = load();
    data.events.push({ ...event, t: Date.now() });
    if (data.events.length > MAX_EVENTS) data.events.splice(0, data.events.length - MAX_EVENTS);
    save(data);
  }

  function bucketize(events, keyFn) {
    const out = {};
    for (const e of events) {
      const k = keyFn(e);
      if (!k) continue;
      out[k] = out[k] || { correct: 0, total: 0 };
      out[k].total++;
      if (e.correct) out[k].correct++;
    }
    for (const k of Object.keys(out)) {
      out[k].accuracy = out[k].correct / out[k].total;
    }
    return out;
  }

  function profile() {
    const { events } = load();
    return {
      total: events.length,
      correct: events.filter((e) => e.correct).length,
      byCategory: bucketize(events, (e) => e.category),
      byRhythm: bucketize(events, (e) => e.rhythm),
      byFocus: bucketize(events, (e) => e.focus),
    };
  }

  // Weakness score per focus area: miss-rate weighted by evidence.
  // Unseen areas get a neutral prior so the generator still explores.
  function weakness(focusKeys) {
    const p = profile();
    const scores = {};
    for (const key of focusKeys) {
      const b = p.byFocus[key];
      if (!b || b.total === 0) {
        scores[key] = 0.5; // unexplored — neutral
      } else {
        const missRate = 1 - b.accuracy;
        const evidence = Math.min(1, b.total / 6);
        scores[key] = 0.5 * (1 - evidence) + missRate * evidence;
      }
    }
    return scores;
  }

  function summaryLine() {
    const p = profile();
    if (p.total < 5) return null;
    const labels = { rhythm: "Rhythm ID", meds: "Meds", condition: "Condition", code: "Code" };
    const parts = [];
    for (const key of Object.keys(labels)) {
      const b = p.byCategory[key];
      if (b && b.total > 0) parts.push(`${labels[key]} ${Math.round(b.accuracy * 100)}%`);
    }
    if (!parts.length) return null;
    return `Your accuracy so far: ${parts.join(" · ")} (${p.total} answers tracked)`;
  }

  function reset() {
    try {
      localStorage.removeItem(STATS_KEY);
    } catch {}
  }

  window.EkgStats = { record, profile, weakness, summaryLine, reset };
})();
