/*
 * Adaptive learning engine: a per-learner mastery model and a spaced-
 * repetition scheduler. No DOM here — ekg-adaptive-ui.js draws it.
 *
 * Mastery model (localStorage, versioned):
 *   - per rhythm, per question topic (category) and per scenario focus:
 *     answers seen, correct, an accuracy moving average, average response
 *     time and when it was last practised;
 *   - per item (a question "q:<id>" or a live strip "s:<rhythm>"): an SM-2
 *     review card (ease, interval, repetitions, due date, lapses);
 *   - which rhythms the learner mistakes for which ("vt>torsades");
 *   - answers per day, for the daily goal and streak.
 *
 * Schema v1 was the plain answer log kept by EkgStats
 * ("ekg-zoll-trainer-stats-v1"). The first load replays that log into the
 * v2 model so nobody's history is lost; the log itself is left in place
 * (the scenario generator still reads it).
 *
 * Every answer recorded through EkgStats.record — quiz, exam, sprint,
 * scenario or adaptive practice — flows in here via EkgStats.onRecord.
 */

(function () {
  const KEY = "ekg-mastery-v2";
  const LEGACY_KEY = "ekg-zoll-trainer-stats-v1";
  const SCHEMA_VERSION = 2;
  const MIN = 60 * 1000;
  const DAY = 24 * 60 * MIN;
  const DAILY_GOAL = 15;
  const SESSION_SIZE = 15;
  const MASTERED = 0.75;
  const LEARNING = 0.45;

  const edu = window.EkgEducation || {};
  const RHYTHM_GUIDE = edu.RHYTHM_GUIDE || {};
  const CONFUSION = edu.SPRINT_CONFUSION || [];
  const QUESTIONS = typeof EKG_QUESTIONS !== "undefined" ? EKG_QUESTIONS : [];
  const RHYTHM_KEYS = Object.keys(RHYTHM_GUIDE);

  // ---------- storage ----------

  function emptyStore(now) {
    return {
      version: SCHEMA_VERSION,
      createdAt: now,
      migratedFrom: null,
      rhythms: {},
      topics: {},
      focus: {},
      items: {},
      confusions: {},
      daily: {},
    };
  }

  function readJSON(key) {
    try {
      return JSON.parse(localStorage.getItem(key));
    } catch {
      return null;
    }
  }

  const isObj = (v) => v !== null && typeof v === "object" && !Array.isArray(v);

  // Bring any stored shape up to the current schema. `legacy` is the v1
  // answer log ({ events: [...] }) used when there is no v2 store yet.
  function migrate(raw, legacy, now = Date.now(), before = Infinity) {
    if (isObj(raw) && raw.version === SCHEMA_VERSION) {
      const s = emptyStore(now);
      for (const k of Object.keys(s)) if (isObj(s[k]) && isObj(raw[k])) s[k] = raw[k];
      s.createdAt = Number.isFinite(raw.createdAt) ? raw.createdAt : now;
      s.migratedFrom = raw.migratedFrom || null;
      if (Number.isFinite(raw.importedEvents)) s.importedEvents = raw.importedEvents;
      return s;
    }
    const s = emptyStore(now);
    const events = isObj(legacy) && Array.isArray(legacy.events) ? legacy.events : [];
    if (events.length) {
      s.migratedFrom = LEGACY_KEY;
      s.importedEvents = 0;
      for (const e of events) {
        if (!isObj(e) || typeof e.correct !== "boolean") continue;
        if (Number.isFinite(e.t) && e.t >= before) continue; // the answer being observed right now
        apply(s, e, Number.isFinite(e.t) ? e.t : now);
        s.importedEvents++;
      }
    }
    return s;
  }

  // `before`: when a live answer triggers the first-ever load, the v1 log
  // already holds that answer; replay only what came before it.
  function load(now = Date.now(), before = Infinity) {
    const raw = readJSON(KEY);
    if (isObj(raw) && raw.version > SCHEMA_VERSION) {
      // Written by a newer build (e.g. an older cached copy after an
      // update): use it, but never overwrite it with an older schema.
      const s = migrate({ ...raw, version: SCHEMA_VERSION }, null, now);
      s.readOnly = true;
      return s;
    }
    const s = migrate(raw, raw ? null : readJSON(LEGACY_KEY), now, before);
    if (!isObj(raw) || raw.version !== SCHEMA_VERSION) save(s); // persist the migration once
    return s;
  }

  function save(s) {
    if (s.readOnly) return;
    try {
      localStorage.setItem(KEY, JSON.stringify(s));
    } catch {}
  }

  function reset() {
    try {
      localStorage.removeItem(KEY);
    } catch {}
  }

  // ---------- mastery math ----------

  // Accuracy moving average: a running mean for the first few answers,
  // then an exponential average so recent answers count for more.
  function updateStat(stat, correct, ms, t) {
    const s = stat || { n: 0, c: 0, ema: 0.5, ms: 0, msN: 0, last: 0 };
    const x = correct ? 1 : 0;
    const alpha = Math.max(0.25, 1 / (s.n + 1));
    s.ema = s.ema + alpha * (x - s.ema);
    s.n++;
    if (correct) s.c++;
    if (Number.isFinite(ms) && ms > 0 && ms < 10 * MIN) {
      s.ms = s.msN === 0 ? ms : s.ms + 0.3 * (ms - s.ms);
      s.msN++;
    }
    s.last = Math.max(s.last || 0, t);
    return s;
  }

  // Confidence grows with evidence: 1 answer → 0.33, 6 → 0.75, 18 → 0.9.
  const confidence = (n) => n / (n + 2);

  // Memory fades without practice; more practice makes it fade slower.
  // Never below half of what was earned.
  function recency(daysSince, n) {
    const halfLife = 7 * (1 + Math.min(n, 20) / 5);
    return 0.5 + 0.5 * Math.pow(2, -Math.max(0, daysSince) / halfLife);
  }

  // Slow answers are less fluent: full credit up to 10 s, sliding to 85%
  // at 40 s.
  function speed(avgMs, samples) {
    if (!samples) return 1;
    const s = avgMs / 1000;
    if (s <= 10) return 1;
    if (s >= 40) return 0.85;
    return 1 - 0.15 * ((s - 10) / 30);
  }

  // 0..1 — what the learner has demonstrably mastered right now.
  function mastery(stat, now = Date.now()) {
    if (!stat || !stat.n) return 0;
    const days = (now - (stat.last || now)) / DAY;
    return stat.ema * confidence(stat.n) * recency(days, stat.n) * speed(stat.ms, stat.msN);
  }

  function level(stat, now = Date.now()) {
    if (!stat || !stat.n) return "new";
    const m = mastery(stat, now);
    return m >= MASTERED ? "mastered" : m >= LEARNING ? "learning" : "weak";
  }

  // Difficulty tier the learner is ready for on a rhythm:
  // 1 recognise it, 2 tell it from look-alikes, 3 manage it.
  function tier(stat, now = Date.now()) {
    const lv = level(stat, now);
    return lv === "mastered" ? 3 : lv === "learning" ? 2 : 1;
  }

  // ---------- SM-2 scheduler ----------

  // Answer quality on SM-2's 0–5 scale from correctness and speed.
  function quality(correct, ms) {
    if (!correct) return 1;
    if (!Number.isFinite(ms) || ms <= 0) return 4;
    if (ms <= 6000) return 5;
    if (ms <= 20000) return 4;
    return 3;
  }

  function newCard() {
    return { ef: 2.5, reps: 0, ivl: 0, due: 0, lapses: 0, n: 0, last: 0 };
  }

  // SM-2 with a short relearning step: a miss comes back in 10 minutes,
  // then 1 day, 3 days, and grows by the ease factor from there.
  function schedule(card, q, now = Date.now()) {
    const c = { ...newCard(), ...(card || {}) };
    c.ef = Math.max(1.3, c.ef + 0.1 - (5 - q) * (0.08 + (5 - q) * 0.02));
    if (q < 3) {
      if (c.reps > 0) c.lapses++;
      c.reps = 0;
      c.ivl = 0;
      c.due = now + 10 * MIN;
    } else {
      c.reps++;
      c.ivl = c.reps === 1 ? 1 : c.reps === 2 ? 3 : Math.round(c.ivl * c.ef);
      c.due = now + c.ivl * DAY;
    }
    c.n++;
    c.last = now;
    return c;
  }

  // ---------- recording ----------

  function dayKey(t) {
    const d = new Date(t);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
  }

  // Fold one answer into a store (shared by live recording and migration).
  function apply(s, e, t) {
    const ms = Number(e.ms);
    if (e.rhythm) s.rhythms[e.rhythm] = updateStat(s.rhythms[e.rhythm], e.correct, ms, t);
    if (e.category) s.topics[e.category] = updateStat(s.topics[e.category], e.correct, ms, t);
    if (e.focus) s.focus[e.focus] = updateStat(s.focus[e.focus], e.correct, ms, t);
    if (typeof e.item === "string" && /^[qs]:/.test(e.item)) {
      s.items[e.item] = schedule(s.items[e.item], quality(e.correct, ms), t);
    }
    if (!e.correct && e.chosen && e.rhythm && e.chosen !== e.rhythm) {
      const k = `${e.rhythm}>${e.chosen}`;
      s.confusions[k] = (s.confusions[k] || 0) + 1;
    }
    const dk = dayKey(t);
    s.daily[dk] = (s.daily[dk] || 0) + 1;
    const days = Object.keys(s.daily).sort();
    for (const old of days.slice(0, Math.max(0, days.length - 60))) delete s.daily[old];
  }

  function observe(e, now = Date.now()) {
    if (!isObj(e) || typeof e.correct !== "boolean") return;
    const s = load(now, now);
    apply(s, e, now);
    save(s);
  }

  // ---------- planning ----------

  function itemPool() {
    const pool = RHYTHM_KEYS.map((k) => ({ id: "s:" + k, kind: "strip", rhythm: k, topic: "rhythm", level: 1 }));
    for (const q of QUESTIONS) {
      pool.push({ id: "q:" + q.id, kind: "question", rhythm: q.rhythm, topic: q.category, level: q.category === "rhythm" ? 2 : 3, q });
    }
    return pool;
  }

  const groupsOf = (key) => CONFUSION.filter((g) => g.includes(key));

  function lookAlikes(key) {
    const out = new Set();
    for (const g of groupsOf(key)) for (const k of g) if (k !== key) out.add(k);
    return [...out];
  }

  // How often this rhythm was mixed up with another, in either direction.
  function confusionCount(s, key) {
    let n = 0;
    for (const [pair, count] of Object.entries(s.confusions)) {
      const [a, b] = pair.split(">");
      if (a === key || b === key) n += count;
    }
    return n;
  }

  // 0..~1.3: how much a rhythm or topic needs work. Unseen material gets a
  // neutral prior, so proven weak spots come before brand-new ones, and
  // misses backed by evidence push it above 1.
  function weakness(stat, now) {
    if (!stat || !stat.n) return 0.6;
    const missRate = 1 - stat.c / stat.n;
    return 1 - mastery(stat, now) + 0.3 * missRate * confidence(stat.n);
  }

  // Priority of a not-yet-due item: weak rhythms and topics first, plus
  // rhythms whose look-alikes are weak (drill the pair together), plus a
  // nudge toward unseen material. Items at the learner's difficulty tier
  // for that rhythm are preferred; harder ones are a stretch.
  function priority(s, item, now) {
    const rs = s.rhythms[item.rhythm];
    const m = mastery(rs, now);
    const weak = weakness(rs, now);
    const topicWeak = weakness(s.topics[item.topic], now);
    let lookAlikeWeak = 0;
    for (const k of lookAlikes(item.rhythm)) {
      const st = s.rhythms[k];
      if (st && st.n) lookAlikeWeak = Math.max(lookAlikeWeak, 1 - mastery(st, now));
    }
    const confused = Math.min(3, confusionCount(s, item.rhythm));
    const card = s.items[item.id];
    const t = tier(rs, now);
    const fit = item.level === t ? 1 : item.level === t + 1 ? 0.8 : item.level < t ? 0.6 : 0.35;
    let score = (weak + 0.5 * topicWeak + 0.35 * lookAlikeWeak + 0.15 * confused + (card ? 0 : 0.1)) * fit;
    if (card && now - card.last < 10 * MIN) score *= 0.2; // just seen
    if (card && m >= MASTERED) score *= 0.3; // already solid and not due
    return { score, lookAlikeWeak, confused, weak: m < LEARNING && rs && rs.n > 0 };
  }

  function reasonFor(item, p, card, now) {
    if (card && card.due <= now) return "due";
    if (p.confused > 0 || p.lookAlikeWeak >= 0.5) return "look-alike";
    if (p.weak) return "weak";
    if (!card) return "new";
    return "practice";
  }

  // Build an adaptive session: due reviews first (at most 60% of it), then
  // the highest-priority items. No more than 3 items per rhythm, and the
  // same rhythm never twice in a row.
  function planSession({ n = SESSION_SIZE, now = Date.now(), rand = Math.random } = {}) {
    const s = load(now);
    const pool = itemPool();
    const scored = pool.map((item) => {
      const card = s.items[item.id];
      const p = priority(s, item, now);
      const due = !!card && card.due <= now;
      const overdue = due ? (now - card.due) / DAY / (card.ivl + 1) : 0;
      return { item, card, p, due, rank: due ? 10 + overdue + rand() * 0.1 : p.score + rand() * 0.15 };
    });
    const dueList = scored.filter((x) => x.due).sort((a, b) => b.rank - a.rank);
    const rest = scored.filter((x) => !x.due).sort((a, b) => b.rank - a.rank);
    const maxDue = Math.ceil(n * 0.6);

    const picked = [];
    const perRhythm = {};
    const take = (x) => {
      if (picked.length >= n || (perRhythm[x.item.rhythm] || 0) >= 3) return;
      perRhythm[x.item.rhythm] = (perRhythm[x.item.rhythm] || 0) + 1;
      picked.push(x);
    };
    for (const x of dueList) if (picked.length < maxDue) take(x);
    for (const x of rest) take(x);
    for (const x of dueList) if (!picked.includes(x)) take(x); // top up if the pool ran short

    // Spread out repeats of a rhythm.
    const ordered = [];
    const queue = picked.slice();
    while (queue.length) {
      const prev = ordered.length ? ordered[ordered.length - 1].item.rhythm : null;
      const i = queue.findIndex((x) => x.item.rhythm !== prev);
      ordered.push(queue.splice(i === -1 ? 0 : i, 1)[0]);
    }

    return ordered.map((x) => ({
      id: x.item.id,
      kind: x.item.kind,
      rhythm: x.item.rhythm,
      topic: x.item.topic,
      q: x.item.q || null,
      reason: reasonFor(x.item, x.p, x.card, now),
      tier: tier(s.rhythms[x.item.rhythm], now),
    }));
  }

  const shuffleWith = (arr, rand) => {
    const a = arr.slice();
    for (let i = a.length - 1; i > 0; i--) {
      const j = Math.floor(rand() * (i + 1));
      [a[i], a[j]] = [a[j], a[i]];
    }
    return a;
  };

  // Four rhythm names for a strip. Tier 1: no look-alikes (learn the
  // pattern). Tier 2: one look-alike. Tier 3: up to three, starting with
  // the rhythms this learner has actually confused it with.
  function stripChoices(key, t, { rand = Math.random, now = Date.now() } = {}) {
    const s = load(now);
    const alikes = lookAlikes(key);
    const personal = Object.entries(s.confusions)
      .filter(([pair]) => pair.startsWith(key + ">"))
      .sort((a, b) => b[1] - a[1])
      .map(([pair]) => pair.split(">")[1])
      .filter((k) => RHYTHM_GUIDE[k] && k !== key);
    const inCount = t >= 3 ? 3 : t === 2 ? 1 : 0;
    const inGroup = [...new Set([...personal, ...shuffleWith(alikes, rand)])].slice(0, inCount);
    const outside = shuffleWith(
      RHYTHM_KEYS.filter((k) => k !== key && !inGroup.includes(k) && (t >= 2 || !alikes.includes(k))),
      rand
    );
    return shuffleWith([key, ...inGroup, ...outside].slice(0, 4), rand);
  }

  // ---------- summaries ----------

  function rhythmMastery(now = Date.now()) {
    const s = load(now);
    return RHYTHM_KEYS.map((k) => {
      const st = s.rhythms[k];
      return {
        key: k,
        name: RHYTHM_GUIDE[k].name,
        mastery: mastery(st, now),
        level: level(st, now),
        n: st ? st.n : 0,
        accuracy: st && st.n ? st.c / st.n : null,
        avgMs: st && st.msN ? st.ms : null,
        last: st ? st.last : 0,
      };
    });
  }

  function topicMastery(now = Date.now()) {
    const s = load(now);
    const out = {};
    for (const [k, st] of Object.entries(s.topics)) out[k] = { mastery: mastery(st, now), level: level(st, now), n: st.n };
    return out;
  }

  function streak(s, now) {
    let days = 0;
    let t = now;
    if (!s.daily[dayKey(t)]) t -= DAY; // today not started yet doesn't break it
    while (s.daily[dayKey(t)]) {
      days++;
      t -= DAY;
    }
    return days;
  }

  // Short "what to study next" plan for the home screen.
  function dailyPlan(now = Date.now()) {
    const s = load(now);
    const rhythms = rhythmMastery(now);
    const dueCount = Object.values(s.items).filter((c) => c.due <= now).length;
    const weak = rhythms
      .filter((r) => r.level === "weak" || r.level === "learning")
      .sort((a, b) => a.mastery - b.mastery)
      .slice(0, 3);
    const unseen = rhythms.filter((r) => r.level === "new");
    const mastered = rhythms.filter((r) => r.level === "mastered").length;
    const doneToday = s.daily[dayKey(now)] || 0;
    const focusOn = weak[0] || unseen[0] || null;

    const steps = [];
    if (dueCount) steps.push({ kind: "review", text: `Review ${dueCount} item${dueCount === 1 ? "" : "s"} due for spaced repetition` });
    if (weak.length) steps.push({ kind: "weak", text: `Strengthen ${weak.map((r) => r.name).join(", ")}` });
    if (unseen.length) {
      const next = unseen.slice(0, 2).map((r) => r.name);
      steps.push({ kind: "new", text: `Meet new rhythm${next.length === 1 ? "" : "s"}: ${next.join(", ")}` });
    }
    if (!steps.length) steps.push({ kind: "maintain", text: "Everything is on schedule — a short round keeps it fresh" });

    return {
      dueCount,
      weak,
      unseenCount: unseen.length,
      mastered,
      total: rhythms.length,
      doneToday,
      goal: DAILY_GOAL,
      streak: streak(s, now),
      steps: steps.slice(0, 3),
      studyKey: focusOn ? focusOn.key : null,
      rhythms,
      hasHistory: rhythms.some((r) => r.n > 0),
    };
  }

  if (window.EkgStats && EkgStats.onRecord) EkgStats.onRecord((e) => observe(e, e.t));
  load(); // migrate an existing v1 history as soon as the app opens

  window.EkgAdaptive = {
    KEY,
    LEGACY_KEY,
    SCHEMA_VERSION,
    DAILY_GOAL,
    SESSION_SIZE,
    load,
    migrate,
    reset,
    observe,
    updateStat,
    mastery,
    level,
    tier,
    recency,
    speed,
    quality,
    schedule,
    planSession,
    stripChoices,
    lookAlikes,
    rhythmMastery,
    topicMastery,
    dailyPlan,
    dayKey,
  };
})();
