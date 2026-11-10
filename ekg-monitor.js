/*
 * Live monitor engine: continuously sweeping EKG + pleth (SpO2) waveforms
 * with a moving erase bar, QRS beep, and lethal-rhythm alarm tones.
 * One controller per canvas; both the quiz and scenario monitors use it.
 *
 * The strip represents STRIP_SECONDS of rhythm; the sweep head redraws it
 * in real time. Stochastic rhythms (afib, VF) get a freshly synthesized
 * strip on every wrap, so they never visibly loop.
 */

(function () {
  const SOUND_KEY = "ekg-zoll-sound-on";
  const GAP_FRAC = 0.02; // erase-bar width as a fraction of the strip

  // Reduced motion: draw each strip whole and hold it (refreshed once per
  // strip period) instead of animating the sweep head across the screen.
  const motionQuery = window.matchMedia ? window.matchMedia("(prefers-reduced-motion: reduce)") : null;
  const reducedMotion = () => !!(motionQuery && motionQuery.matches);

  let audioCtx = null;
  let soundOn = false;
  try {
    soundOn = localStorage.getItem(SOUND_KEY) === "1";
  } catch {}

  function ensureAudio() {
    if (!audioCtx) {
      const AC = window.AudioContext || window.webkitAudioContext;
      if (AC) audioCtx = new AC();
    }
    if (audioCtx && audioCtx.state === "suspended") audioCtx.resume();
  }

  function tone(freq, dur, gain) {
    if (!soundOn || !audioCtx) return;
    const o = audioCtx.createOscillator();
    const g = audioCtx.createGain();
    o.type = "sine";
    o.frequency.value = freq;
    g.gain.setValueAtTime(gain, audioCtx.currentTime);
    g.gain.exponentialRampToValueAtTime(0.0001, audioCtx.currentTime + dur);
    o.connect(g);
    g.connect(audioCtx.destination);
    o.start();
    o.stop(audioCtx.currentTime + dur + 0.02);
  }

  const qrsBeep = () => tone(880, 0.055, 0.05);
  const alarmBeep = () => {
    tone(660, 0.12, 0.045);
    setTimeout(() => tone(520, 0.12, 0.045), 170);
  };

  // R-wave detection on the drawn trace (drives the QRS beep, the pleth and
  // the measured rate). Returns beat times in seconds from the start of
  // `samples`. A peak counts when it clears a threshold scaled to the
  // tallest complex; anything arriving within 0.45 s of a beat must be at
  // least half its height, which is what keeps T waves from counting.
  function detectBeats(samples, perSec = samples.length / EkgRhythms.STRIP_SECONDS) {
    const n = samples.length;
    let tallest = 0;
    for (let i = 0; i < n; i++) if (samples[i] > tallest) tallest = samples[i];
    const threshold = Math.max(0.3, 0.22 * tallest);
    const refractory = 0.2 * perSec;
    const tWindow = 0.45 * perSec;
    const beats = [];
    let last = -Infinity;
    let lastAmp = 0;
    for (let i = 1; i < n - 1; i++) {
      const v = samples[i];
      if (v < threshold || v <= samples[i - 1] || v < samples[i + 1]) continue;
      if (i - last < refractory) continue;
      if (i - last < tWindow && v < 0.5 * lastAmp) continue;
      beats.push(i / perSec);
      last = i;
      lastAmp = v;
    }
    return beats;
  }

  // SpO2 plethysmograph, driven by the beats actually on the trace: each
  // QRS produces a pulse a moment later (fast upstroke, decay with a
  // dicrotic notch). Early beats fill less and pulse weaker, so irregular
  // rhythms show an irregular, uneven pleth. `times` are beat times in
  // seconds relative to the start of the strip (negative = previous strip).
  // Flat when there are no beats.
  function makePleth(times, n) {
    const arr = new Float32Array(n);
    if (!times.length) return arr;
    const dur = EkgRhythms.STRIP_SECONDS;
    const meanRR = times.length > 1 ? (times[times.length - 1] - times[0]) / (times.length - 1) : 1;
    // the trace opens mid-rhythm: assume the same rhythm was running before it
    const t = [times[0] - meanRR, ...times];
    const DELAY = 0.18;
    const UP = 0.11;
    // faster rhythms have a shorter run-off, so the pulse keeps its shape
    const fall = 0.75 * Math.max(0.45, Math.min(1, meanRR / 0.8));
    const pulses = t.map((at, k) => ({
      on: at + DELAY,
      amp: k === 0 ? 1 : Math.max(0.3, Math.min(1, (at - t[k - 1] - 0.22) / 0.4)),
      start: 0,
    }));
    const peak = pulses.reduce((m, p) => Math.max(m, p.amp), 0);
    const value = (p, at) => {
      const d = at - p.on;
      if (d < UP) {
        const u = d / UP;
        return p.start + (p.amp - p.start) * u * u * (3 - 2 * u);
      }
      const x = (d - UP) / fall;
      return p.amp * Math.exp(-3.2 * x) * (1 + 0.18 * Math.sin(10 * x * Math.PI) * Math.exp(-4 * x));
    };
    for (let k = 1; k < pulses.length; k++) pulses[k].start = value(pulses[k - 1], pulses[k].on);
    let k = 0;
    for (let i = 0; i < n; i++) {
      const at = (i / n) * dur;
      while (k + 1 < pulses.length && pulses[k + 1].on <= at) k++;
      arr[i] = at < pulses[k].on ? 0 : value(pulses[k], at) / peak;
    }
    // Auto-gain, as on a real monitor: at fast rates the pulses overlap and
    // the swing shrinks, so stretch it back to a readable (still reduced) size.
    let lo = Infinity;
    let hi = -Infinity;
    for (let i = 0; i < n; i++) {
      if (arr[i] < lo) lo = arr[i];
      if (arr[i] > hi) hi = arr[i];
    }
    const MIN_SWING = 0.55;
    if (hi - lo > 0.02 && hi - lo < MIN_SWING) {
      const gain = MIN_SWING / (hi - lo);
      for (let i = 0; i < n; i++) arr[i] = (arr[i] - lo) * gain;
    }
    return arr;
  }

  // Capnography: square-ish breath waveform — sharp expiratory upstroke,
  // alveolar plateau with a slight upslope, sharp inspiratory downstroke.
  // Amplitude scales with the EtCO2 value (50 mmHg = full scale). Flat when
  // there is no ventilation.
  function makeCapno(rr, level, n) {
    const arr = new Float32Array(n);
    if (!rr || !level) return arr;
    const dur = EkgRhythms.STRIP_SECONDS;
    const breath = 60 / rr;
    const amp = Math.min(1.1, level / 50);
    for (let i = 0; i < n; i++) {
      const t = (i / n) * dur;
      const ph = (t % breath) / breath;
      let v;
      if (ph < 0.06) v = ph / 0.06;
      else if (ph < 0.52) v = 1 + 0.06 * ((ph - 0.06) / 0.46);
      else if (ph < 0.6) v = 1.06 * (1 - (ph - 0.52) / 0.08);
      else v = 0;
      arr[i] = v * amp;
    }
    return arr;
  }

  // The monitor MEASURES its rate like a real bedside monitor: the average
  // of the most recent R-R intervals — 12 when the trace holds that many,
  // otherwise 6. Averaging (rather than taking a typical interval) is what
  // makes grouped rhythms read their true ventricular rate, and 6 and 12
  // are whole numbers of cycles for 2- and 3-beat groupings (bigeminy,
  // 3:2 and 4:3 block), so the number doesn't wobble with where the
  // window happens to start. `times` are beat times in seconds.
  // Null when unmeasurable.
  function measuredRate(times) {
    const have = times.length - 1;
    const m = have >= 12 ? 12 : have >= 6 ? 6 : have;
    if (m < 1) return null;
    const span = times[times.length - 1] - times[times.length - 1 - m];
    return Math.round((60 * m) / span);
  }

  // Rhythms a real monitor flags as unanalyzable rather than rating.
  const NO_RATE = (rhythm) => rhythm === "asystole" || (rhythm || "").startsWith("vf");

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

  // Draw one channel as a swept trace: current strip left of the head,
  // previous strip to the right of the erase gap. anchor "mid" centers the
  // trace in the band (EKG/pleth); "low" sits it on the band's floor (capno).
  function drawChannel(ctx, curr, prev, head, x0, y0, w, h, scale, color, glow, anchor = "mid") {
    const n = curr.length;
    const headX = head * w;
    const gapX = GAP_FRAC * w * 2;
    const baseY = anchor === "low" ? y0 + h - 5 : y0 + h / 2;
    ctx.strokeStyle = color;
    ctx.lineWidth = 2;
    ctx.lineJoin = "round";
    ctx.shadowColor = glow;
    ctx.shadowBlur = 4;

    // Every sample is plotted: stepping by pixels skips over the narrow R
    // peak and makes identical beats draw at different heights.
    const plot = (src, from, to) => {
      ctx.beginPath();
      for (let i = from; i <= to; i++) {
        const x = x0 + (i / n) * w;
        const y = baseY - src[i] * scale;
        if (i === from) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
      }
      ctx.stroke();
    };
    const headI = Math.min(n - 1, Math.floor(head * n));
    plot(curr, 0, headI);
    const resumeI = Math.ceil(((headX + gapX) / w) * n);
    if (resumeI < n) plot(prev, resumeI, n - 1);
    ctx.shadowBlur = 0;

    // bright drawing head
    const hi = Math.min(n - 1, Math.floor(head * n));
    ctx.fillStyle = "#eafff2";
    ctx.beginPath();
    ctx.arc(x0 + headX, baseY - curr[hi] * scale, 2.6, 0, Math.PI * 2);
    ctx.fill();
  }

  function attach(canvasId) {
    const canvas = document.getElementById(canvasId);
    const ctx = canvas.getContext("2d");
    canvas.setAttribute("role", "img");
    const st = {
      rhythm: null,
      curr: null,
      prev: null,
      beats: [],
      plethCurr: null,
      plethPrev: null,
      capnoCurr: null,
      capnoPrev: null,
      hr: 0,
      perfusing: false,
      lethal: false,
      capno: false, // third (EtCO2) waveform band
      rr: 0,
      etco2: null,
      hrEl: null, // element the measured rate is written to
      synth: {}, // generator state: keeps successive strips continuous
      flashUntil: 0, // beat-heart flash deadline (ms timestamp)
      head: 0,
      lastTs: null,
      lastAlarm: 0,
      cssW: 0,
      running: false,
      dirty: true, // reduced-motion: strip changed since the last draw
    };

    // Ventilation drives the capno trace: spontaneous breaths at the charted
    // rate, or ~10/min bag ventilations when EtCO2 is present during arrest.
    function capnoParams() {
      const rate = st.rr > 0 ? st.rr : st.etco2 ? 10 : 0;
      const level = st.etco2 != null ? st.etco2 : st.rr > 0 ? 38 : 0;
      return { rate, level };
    }

    function resynth() {
      // Rhythms synthesize at the charted rate, so the strip, the stem, and
      // the measured number all agree.
      const before = st.curr;
      st.curr = EkgRhythms.synthesizeRhythm(st.rhythm, st.hr || undefined, st.synth);
      const n = st.curr.length;
      const dur = EkgRhythms.STRIP_SECONDS;
      // Beats are read off the previous strip and this one as a single
      // trace, so nothing is missed or double-counted at the wrap.
      let times = [];
      if (!NO_RATE(st.rhythm)) {
        if (before) {
          const joined = new Float32Array(2 * n);
          joined.set(before);
          joined.set(st.curr, n);
          times = detectBeats(joined, n / dur).map((t) => t - dur);
        } else {
          times = detectBeats(st.curr);
        }
      }
      st.beats = times.filter((t) => t >= 0).map((t) => t / dur);
      st.plethCurr = makePleth(st.perfusing ? times : [], n);
      if (st.capno) {
        const { rate, level } = capnoParams();
        st.capnoCurr = makeCapno(rate, level, n);
      }
      if (st.hrEl) {
        const r = measuredRate(times);
        st.hrEl.textContent = r === null ? "--" : r;
      }
    }

    function setRhythm(rhythm, opts = {}) {
      st.rhythm = rhythm;
      // The strip is the question — don't name the rhythm, just describe it.
      canvas.setAttribute(
        "aria-label",
        `Simulated lead II EKG strip${opts.perfusing ? " with SpO2 pleth waveform" : ""}` +
          (opts.hr ? `, monitor heart rate ${opts.hr}` : "")
      );
      st.hr = opts.hr || 0;
      st.perfusing = !!opts.perfusing;
      st.lethal = !!opts.lethal;
      st.capno = !!opts.capno;
      st.rr = opts.rr || 0;
      st.etco2 = opts.etco2 != null ? opts.etco2 : null;
      st.hrEl = opts.hrEl || null;
      st.synth = {};
      st.curr = null;
      resynth();
      st.prev = st.curr;
      st.plethPrev = st.plethCurr;
      st.capnoPrev = st.capnoCurr;
      resynth();
      st.head = 0;
      st.lastTs = null;
      st.dirty = true;
      if (!st.running) {
        st.running = true;
        requestAnimationFrame(frame);
      }
    }

    function frame(ts) {
      if (!st.running) return;
      requestAnimationFrame(frame);
      if (st.lastTs === null) {
        st.lastTs = ts;
        return;
      }
      const dt = Math.min(0.1, (ts - st.lastTs) / 1000);
      st.lastTs = ts;

      const prevHead = st.head;
      st.head += dt / EkgRhythms.STRIP_SECONDS;

      // Beats the head just crossed: QRS beep + the beat-heart flash
      for (const b of st.beats) {
        if (b > prevHead && b <= st.head) {
          st.flashUntil = ts + 150;
          if (soundOn && audioCtx && canvas.offsetParent !== null) qrsBeep();
        }
      }
      if (soundOn && audioCtx && canvas.offsetParent !== null && st.lethal && ts - st.lastAlarm > 1800) {
        alarmBeep();
        st.lastAlarm = ts;
      }

      if (st.head >= 1) {
        st.head -= 1;
        st.prev = st.curr;
        st.plethPrev = st.plethCurr;
        st.capnoPrev = st.capnoCurr;
        resynth(); // fresh strip: stochastic rhythms never visibly loop
        st.dirty = true;
      }

      if (canvas.offsetParent === null) {
        st.dirty = true; // redraw as soon as it's shown again
        return; // hidden — skip drawing
      }

      // Backing store tracks the rendered size (handles rotation/resizes
      // and keeps the trace crisp on high-DPR phones).
      const dpr = window.devicePixelRatio || 1;
      const cssW = canvas.clientWidth || 560;
      const cssH = canvas.clientHeight || 150;
      if (canvas.width !== Math.round(cssW * dpr) || canvas.height !== Math.round(cssH * dpr)) {
        canvas.width = Math.round(cssW * dpr);
        canvas.height = Math.round(cssH * dpr);
        st.dirty = true;
      }

      const still = reducedMotion();
      if (still && !st.dirty) return;
      st.dirty = false;
      const head = still ? 1 : st.head;
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

      drawGrid(ctx, cssW, cssH);

      // Band layout: EKG on top, SpO2 pleth below (red, as on a Zoll),
      // and an EtCO2 capnograph (yellow) when the monitor has one on.
      const ekgH = cssH * (st.capno ? 0.52 : 0.68);
      const plethH = st.capno ? cssH * 0.26 : cssH - ekgH;
      const capnoH = st.capno ? cssH - ekgH - plethH : 0;

      drawChannel(ctx, st.curr, st.prev, head, 0, 0, cssW, ekgH, ekgH * 0.34, "#39ff8f", "rgba(57,255,143,0.55)");
      drawChannel(
        ctx,
        st.plethCurr,
        st.plethPrev,
        head,
        0,
        ekgH,
        cssW,
        plethH,
        plethH * 0.62,
        "#ff6b6b",
        "rgba(255,107,107,0.5)"
      );
      if (st.capno) {
        drawChannel(
          ctx,
          st.capnoCurr,
          st.capnoPrev,
          head,
          0,
          ekgH + plethH,
          cssW,
          capnoH,
          (capnoH - 8) * 0.82,
          "#ffd23f",
          "rgba(255,210,63,0.45)",
          "low"
        );
      }

      // channel dividers + labels, like the real screen's band annotations
      ctx.strokeStyle = "rgba(255,255,255,0.07)";
      ctx.beginPath();
      ctx.moveTo(0, ekgH + 0.5);
      ctx.lineTo(cssW, ekgH + 0.5);
      if (st.capno) {
        ctx.moveTo(0, ekgH + plethH + 0.5);
        ctx.lineTo(cssW, ekgH + plethH + 0.5);
      }
      ctx.stroke();
      ctx.font = "9px sans-serif";
      ctx.fillStyle = "rgba(57,255,143,0.8)";
      ctx.fillText("II  1.0 mV", 6, 11);
      ctx.fillStyle = "rgba(255,107,107,0.8)";
      ctx.fillText("SpO2", 6, ekgH + 11);
      if (st.capno) {
        ctx.fillStyle = "rgba(255,210,63,0.8)";
        ctx.fillText("EtCO2", 6, ekgH + plethH + 11);
      }

      // beat heart: flashes with each QRS the sweep head crosses
      if (!still && ts < st.flashUntil) {
        ctx.fillStyle = "#39ff8f";
        ctx.font = "15px sans-serif";
        ctx.fillText("♥", cssW - 22, 16);
      }
    }

    function stop() {
      st.running = false;
    }

    return { setRhythm, stop };
  }

  // ---------- sound toggle (shared across all monitors) ----------

  function refreshToggles() {
    document.querySelectorAll(".sound-toggle").forEach((b) => {
      b.textContent = soundOn ? "\u{1F50A}" : "\u{1F507}";
      b.title = soundOn ? "Monitor sound on" : "Monitor sound off — click for QRS beep & alarms";
      b.setAttribute("aria-pressed", soundOn ? "true" : "false");
    });
  }

  document.addEventListener("click", (e) => {
    const b = e.target.closest(".sound-toggle");
    if (!b) return;
    soundOn = !soundOn;
    try {
      localStorage.setItem(SOUND_KEY, soundOn ? "1" : "0");
    } catch {}
    if (soundOn) ensureAudio();
    refreshToggles();
  });

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", refreshToggles);
  } else {
    refreshToggles();
  }

  window.EkgMonitor = { attach, detectBeats, measuredRate, makePleth };
})();
