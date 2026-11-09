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

  // Simple R-peak detection on a synthesized strip (drives the QRS beep).
  function detectBeats(samples) {
    const n = samples.length;
    const minDist = Math.floor(n / 30); // 0.2 s refractory
    const beats = [];
    let last = -minDist;
    for (let i = 1; i < n - 1; i++) {
      const v = samples[i];
      if (v > 0.45 && v >= samples[i - 1] && v >= samples[i + 1] && i - last >= minDist) {
        beats.push(i / n);
        last = i;
      }
    }
    return beats;
  }

  // SpO2 plethysmograph: fast systolic upstroke, exponential decay with a
  // dicrotic notch. Flat when not perfusing.
  function makePleth(hr, n) {
    const arr = new Float32Array(n);
    if (!hr || hr <= 0) return arr;
    const dur = EkgRhythms.STRIP_SECONDS;
    const beat = 60 / hr;
    for (let i = 0; i < n; i++) {
      const t = (i / n) * dur;
      const ph = (t % beat) / beat;
      let v;
      if (ph < 0.12) v = ph / 0.12;
      else {
        const d = (ph - 0.12) / 0.88;
        v = Math.exp(-3.2 * d) * (1 + 0.18 * Math.sin(10 * d * Math.PI) * Math.exp(-4 * d));
      }
      arr[i] = v;
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

  // The monitor MEASURES its rate like a real Zoll: median R-R interval of
  // the detected QRS complexes on the current strip. Null when unmeasurable.
  function measuredRate(beats) {
    if (beats.length < 2) return null;
    const iv = [];
    for (let i = 1; i < beats.length; i++) iv.push((beats[i] - beats[i - 1]) * EkgRhythms.STRIP_SECONDS);
    iv.sort((a, b) => a - b);
    return Math.round(60 / iv[Math.floor(iv.length / 2)]);
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

    ctx.beginPath();
    let started = false;
    for (let x = 0; x <= headX; x += 1.5) {
      const i = Math.min(n - 1, Math.floor((x / w) * n));
      const y = baseY - curr[i] * scale;
      if (!started) {
        ctx.moveTo(x0 + x, y);
        started = true;
      } else ctx.lineTo(x0 + x, y);
    }
    ctx.stroke();

    ctx.beginPath();
    started = false;
    for (let x = headX + gapX; x <= w; x += 1.5) {
      const i = Math.min(n - 1, Math.floor((x / w) * n));
      const y = baseY - prev[i] * scale;
      if (!started) {
        ctx.moveTo(x0 + x, y);
        started = true;
      } else ctx.lineTo(x0 + x, y);
    }
    ctx.stroke();
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
      // Rate-flexible rhythms synthesize at the charted rate, so the strip,
      // the stem, and the measured number all agree.
      st.curr = EkgRhythms.synthesizeRhythm(st.rhythm, st.hr || undefined);
      st.beats = detectBeats(st.curr);
      st.plethCurr = makePleth(st.perfusing ? st.hr : 0, st.curr.length);
      if (st.capno) {
        const { rate, level } = capnoParams();
        st.capnoCurr = makeCapno(rate, level, st.curr.length);
      }
      if (st.hrEl) {
        const r = NO_RATE(st.rhythm) ? null : measuredRate(st.beats);
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

  window.EkgMonitor = { attach };
})();
