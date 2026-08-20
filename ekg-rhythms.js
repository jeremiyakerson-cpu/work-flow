/*
 * Waveform synthesis for the Zoll-style EKG trainer.
 * Produces a Float32Array (range roughly -1.3..1.3) representing one
 * lead-II strip, sampled evenly across a fixed time window.
 */

const STRIP_SECONDS = 6;
const SAMPLE_COUNT = 1200; // internal resolution; canvas rescales on draw

function gauss(t, width) {
  return Math.exp(-(t * t) / (2 * width * width));
}

function pWave(t, amp) {
  return amp * gauss(t, 0.045);
}

function tWave(t, amp) {
  return amp * gauss(t, 0.09);
}

function qrsNarrow(t, amp) {
  const q = -0.15 * amp * gauss(t + 0.02, 0.008);
  const r = amp * gauss(t, 0.014);
  const s = -0.3 * amp * gauss(t - 0.025, 0.012);
  return q + r + s;
}

function qrsWide(t, amp) {
  const r = amp * gauss(t + 0.01, 0.045);
  const s = -0.55 * amp * gauss(t - 0.07, 0.045);
  return r + s;
}

// One PQRST complex centered so the R (or wide-QRS peak) sits at t=0.
function beatValue(t, opts) {
  const {
    amp = 1,
    pr = 0.16,
    includeP = true,
    tAmp = 0.28,
    wide = false,
    includeT = true,
    pAmp = 0.15,
  } = opts;
  let v = 0;
  if (includeP) v += pWave(t + pr, pAmp);
  v += wide ? qrsWide(t, amp) : qrsNarrow(t, amp);
  if (includeT) v += tWave(t - (wide ? 0.32 : 0.28), wide ? tAmp * 1.3 : tAmp);
  return v;
}

function regularTimes(rate, duration, startOffset = 0.45) {
  const rr = 60 / rate;
  const out = [];
  let t = startOffset;
  while (t < duration - 0.35) {
    out.push(t);
    t += rr;
  }
  return out;
}

function makeArray() {
  return new Float32Array(SAMPLE_COUNT);
}

function addBeats(arr, beats, duration, beatOpts) {
  const dt = duration / SAMPLE_COUNT;
  for (const b of beats) {
    const opts = typeof b === "number" ? beatOpts : { ...beatOpts, ...(b.opts || {}) };
    const center = typeof b === "number" ? b : b.t;
    const lo = Math.max(0, Math.floor((center - 0.5) / dt));
    const hi = Math.min(SAMPLE_COUNT - 1, Math.ceil((center + 0.5) / dt));
    for (let i = lo; i <= hi; i++) {
      const t = i * dt - center;
      arr[i] += beatValue(t, opts);
    }
  }
}

function addFn(arr, duration, fn) {
  const dt = duration / SAMPLE_COUNT;
  for (let i = 0; i < SAMPLE_COUNT; i++) {
    arr[i] += fn(i * dt);
  }
}

const RHYTHMS = {
  nsr: () => {
    const arr = makeArray();
    addBeats(arr, regularTimes(76, STRIP_SECONDS), STRIP_SECONDS, { amp: 1, pr: 0.15 });
    return arr;
  },

  sinus_brady: () => {
    const arr = makeArray();
    addBeats(arr, regularTimes(44, STRIP_SECONDS), STRIP_SECONDS, { amp: 1, pr: 0.18 });
    return arr;
  },

  sinus_tach: () => {
    const arr = makeArray();
    addBeats(arr, regularTimes(122, STRIP_SECONDS), STRIP_SECONDS, { amp: 0.95, pr: 0.13, tAmp: 0.22 });
    return arr;
  },

  svt: () => {
    const arr = makeArray();
    addBeats(arr, regularTimes(186, STRIP_SECONDS), STRIP_SECONDS, {
      amp: 0.9,
      includeP: false,
      tAmp: 0.18,
    });
    return arr;
  },

  afib: () => {
    const arr = makeArray();
    // Fibrillatory baseline: several irregular low-amplitude sine components.
    const freqs = [5.3, 6.7, 8.1, 4.4].map((f) => f + (Math.random() - 0.5));
    const phases = freqs.map(() => Math.random() * Math.PI * 2);
    addFn(arr, STRIP_SECONDS, (t) => {
      let v = 0;
      freqs.forEach((f, i) => (v += 0.045 * Math.sin(2 * Math.PI * f * t + phases[i])));
      return v;
    });
    // Irregularly irregular QRS timing, no P waves.
    const beats = [];
    let t = 0.4;
    while (t < STRIP_SECONDS - 0.35) {
      beats.push(t);
      t += 60 / (95 + Math.random() * 70);
    }
    addBeats(arr, beats, STRIP_SECONDS, { amp: 0.85, includeP: false, tAmp: 0.15 });
    return arr;
  },

  aflutter: () => {
    const arr = makeArray();
    const period = 0.2; // ~300/min flutter waves
    addFn(arr, STRIP_SECONDS, (t) => {
      const phase = (t % period) / period;
      // Sawtooth "picket fence" shape
      return 0.22 * (1 - 2 * Math.abs(phase - 0.5)) * 2 - 0.12;
    });
    // Narrow QRS every 4th flutter wave (~75/min ventricular rate, 4:1 conduction)
    const beats = [];
    let t = 0.5;
    while (t < STRIP_SECONDS - 0.35) {
      beats.push(t);
      t += period * 4;
    }
    addBeats(arr, beats, STRIP_SECONDS, { amp: 0.8, includeP: false, tAmp: 0.15 });
    return arr;
  },

  avb1: () => {
    const arr = makeArray();
    addBeats(arr, regularTimes(70, STRIP_SECONDS), STRIP_SECONDS, { amp: 1, pr: 0.32 });
    return arr;
  },

  mobitz1: () => {
    const arr = makeArray();
    const rrP = 60 / 78;
    const dt = STRIP_SECONDS / SAMPLE_COUNT;
    const pTimes = regularTimes(78, STRIP_SECONDS);
    const prSteps = [0.15, 0.22, 0.3];
    const beats = [];
    pTimes.forEach((pt, i) => {
      const pos = i % 4;
      if (pos < 3) {
        beats.push(pt + prSteps[pos]);
      }
      // pos === 3: P wave present, QRS dropped
    });
    // Draw all P waves independently (some won't have a following QRS)
    for (let i = 0; i < SAMPLE_COUNT; i++) {
      const t = i * dt;
      let v = 0;
      for (const pt of pTimes) {
        if (Math.abs(t - pt) < 0.25) v += pWave(t - pt, 0.15);
      }
      arr[i] += v;
    }
    addBeats(arr, beats, STRIP_SECONDS, { amp: 0.95, includeP: false, tAmp: 0.22 });
    return arr;
  },

  mobitz2: () => {
    const arr = makeArray();
    const dt = STRIP_SECONDS / SAMPLE_COUNT;
    const pTimes = regularTimes(78, STRIP_SECONDS);
    const beats = [];
    pTimes.forEach((pt, i) => {
      if (i % 3 !== 2) beats.push(pt + 0.16);
    });
    for (let i = 0; i < SAMPLE_COUNT; i++) {
      const t = i * dt;
      let v = 0;
      for (const pt of pTimes) {
        if (Math.abs(t - pt) < 0.25) v += pWave(t - pt, 0.15);
      }
      arr[i] += v;
    }
    addBeats(arr, beats, STRIP_SECONDS, { amp: 0.95, includeP: false, tAmp: 0.22 });
    return arr;
  },

  avb3: () => {
    const arr = makeArray();
    const dt = STRIP_SECONDS / SAMPLE_COUNT;
    const pTimes = regularTimes(95, STRIP_SECONDS, 0.2);
    for (let i = 0; i < SAMPLE_COUNT; i++) {
      const t = i * dt;
      let v = 0;
      for (const pt of pTimes) {
        if (Math.abs(t - pt) < 0.25) v += pWave(t - pt, 0.13);
      }
      arr[i] += v;
    }
    // Independent, slower ventricular escape rhythm — wide, unrelated to P timing.
    addBeats(arr, regularTimes(36, STRIP_SECONDS, 0.55), STRIP_SECONDS, {
      amp: 0.9,
      includeP: false,
      wide: true,
      tAmp: 0.2,
    });
    return arr;
  },

  junctional: () => {
    const arr = makeArray();
    addBeats(arr, regularTimes(48, STRIP_SECONDS), STRIP_SECONDS, { amp: 0.9, includeP: false, tAmp: 0.22 });
    return arr;
  },

  pvc_bigeminy: () => {
    const arr = makeArray();
    const rrN = 60 / 76;
    const beats = [];
    let t = 0.5;
    let isPvc = false;
    while (t < STRIP_SECONDS - 0.35) {
      beats.push({ t, opts: isPvc ? { amp: 1.1, includeP: false, wide: true, tAmp: 0.3 } : { amp: 1, pr: 0.15 } });
      t += isPvc ? rrN * 1.3 : rrN * 0.6;
      isPvc = !isPvc;
    }
    addBeats(arr, beats, STRIP_SECONDS, { amp: 1, pr: 0.15 });
    return arr;
  },

  vt: () => {
    const arr = makeArray();
    addBeats(arr, regularTimes(178, STRIP_SECONDS), STRIP_SECONDS, {
      amp: 1.1,
      includeP: false,
      wide: true,
      tAmp: 0.25,
    });
    return arr;
  },

  torsades: () => {
    const arr = makeArray();
    const dt = STRIP_SECONDS / SAMPLE_COUNT;
    const beats = regularTimes(220, STRIP_SECONDS);
    for (const bt of beats) {
      const envelope = Math.sin((2 * Math.PI * bt) / 1.4);
      const lo = Math.max(0, Math.floor((bt - 0.3) / dt));
      const hi = Math.min(SAMPLE_COUNT - 1, Math.ceil((bt + 0.3) / dt));
      for (let i = lo; i <= hi; i++) {
        const t = i * dt - bt;
        arr[i] += beatValue(t, { amp: 0.95 * envelope, includeP: false, wide: true, includeT: false });
      }
    }
    return arr;
  },

  vf_coarse: () => {
    const arr = makeArray();
    const freqs = [3.1, 4.7, 6.3, 7.9, 2.4].map((f) => f + Math.random());
    const phases = freqs.map(() => Math.random() * Math.PI * 2);
    addFn(arr, STRIP_SECONDS, (t) => {
      let v = 0;
      freqs.forEach((f, i) => (v += (0.9 / freqs.length) * Math.sin(2 * Math.PI * f * t + phases[i])));
      v *= 0.7 + 0.3 * Math.sin(2 * Math.PI * 0.6 * t);
      return v;
    });
    return arr;
  },

  vf_fine: () => {
    const arr = makeArray();
    const freqs = [3.1, 4.7, 6.3, 7.9, 2.4].map((f) => f + Math.random());
    const phases = freqs.map(() => Math.random() * Math.PI * 2);
    addFn(arr, STRIP_SECONDS, (t) => {
      let v = 0;
      freqs.forEach((f, i) => (v += (0.32 / freqs.length) * Math.sin(2 * Math.PI * f * t + phases[i])));
      return v;
    });
    return arr;
  },

  asystole: () => {
    const arr = makeArray();
    addFn(arr, STRIP_SECONDS, () => (Math.random() - 0.5) * 0.03);
    return arr;
  },
};

function synthesizeRhythm(key) {
  const fn = RHYTHMS[key];
  if (!fn) return RHYTHMS.nsr();
  return fn();
}

window.EkgRhythms = { synthesizeRhythm, STRIP_SECONDS, SAMPLE_COUNT };
