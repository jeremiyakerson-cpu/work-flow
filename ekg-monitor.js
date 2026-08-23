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
  // previous strip to the right of the erase gap.
  function drawChannel(ctx, curr, prev, head, x0, y0, w, h, scale, color, glow) {
    const n = curr.length;
    const headX = head * w;
    const gapX = GAP_FRAC * w * 2;
    const midY = y0 + h / 2;
    ctx.strokeStyle = color;
    ctx.lineWidth = 2;
    ctx.lineJoin = "round";
    ctx.shadowColor = glow;
    ctx.shadowBlur = 4;

    ctx.beginPath();
    let started = false;
    for (let x = 0; x <= headX; x += 1.5) {
      const i = Math.min(n - 1, Math.floor((x / w) * n));
      const y = midY - curr[i] * scale;
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
      const y = midY - prev[i] * scale;
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
    ctx.arc(x0 + headX, midY - curr[hi] * scale, 2.6, 0, Math.PI * 2);
    ctx.fill();
  }

  function attach(canvasId) {
    const canvas = document.getElementById(canvasId);
    const ctx = canvas.getContext("2d");
    const st = {
      rhythm: null,
      curr: null,
      prev: null,
      beats: [],
      plethCurr: null,
      plethPrev: null,
      hr: 0,
      perfusing: false,
      lethal: false,
      head: 0,
      lastTs: null,
      lastAlarm: 0,
      cssW: 0,
      running: false,
    };

    function resynth() {
      st.curr = EkgRhythms.synthesizeRhythm(st.rhythm);
      st.beats = detectBeats(st.curr);
      st.plethCurr = makePleth(st.perfusing ? st.hr : 0, st.curr.length);
    }

    function setRhythm(rhythm, opts = {}) {
      st.rhythm = rhythm;
      st.hr = opts.hr || 0;
      st.perfusing = !!opts.perfusing;
      st.lethal = !!opts.lethal;
      resynth();
      st.prev = st.curr;
      st.plethPrev = st.plethCurr;
      resynth();
      st.head = 0;
      st.lastTs = null;
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

      // QRS beeps for beats the head just crossed
      if (soundOn && audioCtx && canvas.offsetParent !== null) {
        for (const b of st.beats) {
          if (b > prevHead && b <= st.head) qrsBeep();
        }
        if (st.lethal && ts - st.lastAlarm > 1800) {
          alarmBeep();
          st.lastAlarm = ts;
        }
      }

      if (st.head >= 1) {
        st.head -= 1;
        st.prev = st.curr;
        st.plethPrev = st.plethCurr;
        resynth(); // fresh strip: stochastic rhythms never visibly loop
      }

      if (canvas.offsetParent === null) return; // hidden — skip drawing

      const dpr = window.devicePixelRatio || 1;
      const cssW = canvas.clientWidth || 560;
      const cssH = canvas.clientHeight || 150;
      if (canvas.width !== Math.round(cssW * dpr) || canvas.height !== Math.round(cssH * dpr)) {
        canvas.width = Math.round(cssW * dpr);
        canvas.height = Math.round(cssH * dpr);
      }
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

      drawGrid(ctx, cssW, cssH);

      const ekgH = cssH * 0.68;
      const plethH = cssH - ekgH;

      drawChannel(ctx, st.curr, st.prev, st.head, 0, 0, cssW, ekgH, ekgH * 0.34, "#39ff8f", "rgba(57,255,143,0.55)");
      drawChannel(
        ctx,
        st.plethCurr,
        st.plethPrev,
        st.head,
        0,
        ekgH,
        cssW,
        plethH,
        plethH * 0.62,
        "#4fd1ff",
        "rgba(79,209,255,0.5)"
      );

      // channel divider + pleth label
      ctx.strokeStyle = "rgba(255,255,255,0.07)";
      ctx.beginPath();
      ctx.moveTo(0, ekgH + 0.5);
      ctx.lineTo(cssW, ekgH + 0.5);
      ctx.stroke();
      ctx.fillStyle = "rgba(79,209,255,0.75)";
      ctx.font = "9px sans-serif";
      ctx.fillText("PLETH SpO2", 6, ekgH + 11);
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
