/*
 * Waveform synthesis for the Zoll-style EKG trainer.
 * Produces a Float32Array (range roughly -1.3..1.3, 1.0 ≈ 1 mV) representing
 * one lead-II strip, sampled evenly across a fixed time window.
 *
 * Intervals follow the textbook definitions so a learner can measure them
 * off the strip: `pr` is P-wave ONSET to QRS onset, a narrow QRS is ~0.08 s,
 * a ventricular (wide) QRS ~0.15 s with a discordant T wave, and the QT
 * shortens with rate so P waves stay visible in sinus tachycardia.
 *
 * Successive strips of the same rhythm are continuous in time: pass the
 * same `state` object back in and the beat timing carries across the wrap,
 * so the live monitor never shows a pause at the edge of the screen.
 */

const STRIP_SECONDS = 6;
const SAMPLE_COUNT = 1200; // internal resolution; canvas rescales on draw
const DT = STRIP_SECONDS / SAMPLE_COUNT;

const QRS_ONSET = 0.04; // narrow QRS begins this long before its R peak
const WIDE_ONSET = 0.05; // same, for a ventricular complex
const P_HALF = 0.045; // P onset to P peak (P wave ≈ 0.09 s)
const LEAD_IN = 1.4; // beats this far before the strip can still reach into it
const TAIL = 0.8;

function clamp(v, lo, hi) {
  return Math.max(lo, Math.min(hi, v));
}

function gauss(t, width) {
  return Math.exp(-(t * t) / (2 * width * width));
}

// Bump with different widths either side of its peak.
function skew(t, left, right) {
  return gauss(t, t < 0 ? left : right);
}

function pWave(t, amp) {
  return amp * gauss(t, 0.021);
}

function qrsNarrow(t, amp) {
  const q = -0.12 * amp * gauss(t + 0.022, 0.007);
  const r = amp * gauss(t, 0.012);
  const s = -0.25 * amp * gauss(t - 0.024, 0.009);
  return q + r + s;
}

function qrsWide(t, amp) {
  const r = amp * skew(t, 0.024, 0.03);
  const s = -0.1 * amp * gauss(t - 0.075, 0.015);
  return r + s;
}

// QT for a given R-R interval (Bazett, QTc 0.40 s; ventricular beats run longer).
function qtFor(rr, wide) {
  return wide ? clamp(0.46 * Math.sqrt(rr), 0.25, 0.52) : clamp(0.4 * Math.sqrt(rr), 0.2, 0.46);
}

// One complex with its R (or wide-QRS) peak at t=0.
//   pr:   P onset → QRS onset in seconds; null/undefined draws no P wave
//   qt:   QRS onset → end of T
//   wide: ventricular morphology — wide QRS, T wave opposite the QRS
function beatValue(t, b) {
  const amp = b.amp === undefined ? 1 : b.amp;
  let v = 0;
  if (b.wide) {
    v += qrsWide(t, amp);
    const s = clamp(b.qt / 0.44, 0.55, 1.25);
    const right = 0.045 * s;
    const peak = -WIDE_ONSET + b.qt - 2.1 * right;
    // the ST segment slopes straight off the QRS into the inverted T
    const left = Math.max(0.03, (peak - 0.04) / 2.2);
    v -= (b.tAmp === undefined ? 0.45 : b.tAmp) * amp * skew(t - peak, left, right);
    return v;
  }
  v += qrsNarrow(t, amp);
  const s = clamp(b.qt / 0.38, 0.5, 1.25);
  const right = 0.036 * s;
  const tEnd = -QRS_ONSET + b.qt;
  v += (b.tAmp === undefined ? 0.25 : b.tAmp) * skew(t - (tEnd - 2.1 * right), 0.062 * s, right);
  if (b.pr != null) v += pWave(t + QRS_ONSET + b.pr - P_HALF, b.pAmp === undefined ? 0.15 : b.pAmp);
  return v;
}

function makeArray() {
  const arr = new Float32Array(SAMPLE_COUNT);
  arr.qrs = []; // true QRS times (s) on this strip — what a learner would count
  return arr;
}

// Draw beats ({t, ...beatValue options}, t in strip seconds; may fall outside
// the strip so complexes are cut naturally at the edges). `rr` sets the QT.
function addBeats(arr, beats, rr, shared) {
  for (const raw of beats) {
    const b = { ...shared, ...raw };
    if (b.qt === undefined) b.qt = qtFor(rr, b.wide);
    if (b.t >= 0 && b.t < STRIP_SECONDS) arr.qrs.push(b.t);
    const lo = Math.max(0, Math.floor((b.t - 0.6) / DT));
    const hi = Math.min(SAMPLE_COUNT - 1, Math.ceil((b.t + 0.8) / DT));
    for (let i = lo; i <= hi; i++) arr[i] += beatValue(i * DT - b.t, b);
  }
  arr.qrs.sort((a, b) => a - b);
}

function addPWaves(arr, onsets, amp) {
  for (const on of onsets) {
    const c = on + P_HALF;
    const lo = Math.max(0, Math.floor((c - 0.15) / DT));
    const hi = Math.min(SAMPLE_COUNT - 1, Math.ceil((c + 0.15) / DT));
    for (let i = lo; i <= hi; i++) arr[i] += pWave(i * DT - c, amp);
  }
}

function addFn(arr, fn) {
  for (let i = 0; i < SAMPLE_COUNT; i++) arr[i] += fn(i * DT);
}

// A running series of events (beats, P waves) in absolute time that persists
// in `st` across strips. Returns the events relevant to the current strip,
// with `t` converted to strip seconds.
function series(st, name, first, next) {
  const end = st.t0 + STRIP_SECONDS + TAIL;
  let ev = st[name];
  if (!ev) ev = st[name] = [first()];
  while (ev[ev.length - 1].t < end) ev.push(next(ev[ev.length - 1]));
  while (ev.length > 1 && ev[1].t < st.t0 - LEAD_IN) ev.shift();
  return ev.map((e) => ({ ...e, t: e.t - st.t0 }));
}

// Evenly spaced events at `period`, starting before the strip at a random phase.
function steady(st, name, period) {
  return series(
    st,
    name,
    () => ({ t: Math.random() * period - period * Math.ceil((period + LEAD_IN) / period), i: 0 }),
    (p) => ({ t: p.t + period, i: p.i + 1 })
  );
}

function regularRhythm(st, rate, beat) {
  const arr = makeArray();
  const rr = 60 / clamp(rate, 20, 260);
  addBeats(arr, steady(st, "qrs", rr), rr, beat);
  return arr;
}

// Ventricular fibrillation: several drifting frequency components with a
// slowly wandering envelope — no repeating complex anywhere.
function fibrillation(total, speed, ceiling) {
  const arr = makeArray();
  const parts = [2.6, 3.4, 4.3, 5.2, 6.4].map((f, i) => ({
    f: (f + Math.random() * 0.8) * speed,
    ph: Math.random() * Math.PI * 2,
    wf: 0.25 + 0.12 * i + Math.random() * 0.1,
    wph: Math.random() * Math.PI * 2,
  }));
  const e1 = Math.random() * Math.PI * 2;
  const e2 = Math.random() * Math.PI * 2;
  addFn(arr, (t) => {
    let v = 0;
    for (const p of parts) {
      v += (total / parts.length) * Math.sin(2 * Math.PI * p.f * t + p.ph + 0.9 * Math.sin(2 * Math.PI * p.wf * t + p.wph));
    }
    v *= 0.8 + 0.2 * Math.sin(2 * Math.PI * 0.45 * t + e1) * Math.sin(2 * Math.PI * 0.19 * t + e2);
    // fine VF never reaches the height of a real complex
    return ceiling ? ceiling * Math.tanh(v / ceiling) : v;
  });
  return arr;
}

// Each generator takes (state, rate). `rate` is the VENTRICULAR rate the
// caller wants on the monitor; every rhythm has a clinically typical default.
const RHYTHMS = {
  nsr: (st, rate = 76) => regularRhythm(st, rate, { pr: 0.16 }),

  sinus_brady: (st, rate = 44) => regularRhythm(st, rate, { pr: 0.16 }),

  sinus_tach: (st, rate = 122) => regularRhythm(st, rate, { amp: 0.95, pr: 0.14, tAmp: 0.2 }),

  svt: (st, rate = 186) => regularRhythm(st, rate, { amp: 0.9, tAmp: 0.16 }),

  afib: (st, rate = 128) => {
    const arr = makeArray();
    // Fine fibrillatory baseline: fast, low, never repeating — no P waves.
    const parts = [5.4, 6.6, 7.9, 9.1].map((f) => ({
      f: f + (Math.random() - 0.5),
      ph: Math.random() * Math.PI * 2,
      mph: Math.random() * Math.PI * 2,
    }));
    addFn(arr, (t) => {
      let v = 0;
      for (const p of parts) {
        v += 0.03 * (0.6 + 0.4 * Math.sin(2 * Math.PI * 0.5 * t + p.mph)) * Math.sin(2 * Math.PI * p.f * t + p.ph);
      }
      return v;
    });
    // Irregularly irregular R-R, averaging the requested ventricular rate.
    const base = 60 / clamp(rate, 50, 180);
    const gap = () => base / (0.72 + 0.56 * Math.random()) / 1.0275;
    const beats = series(
      st,
      "qrs",
      () => ({ t: -LEAD_IN - Math.random() * base }),
      (p) => ({ t: p.t + gap() })
    );
    addBeats(arr, beats, base, { amp: 0.85, tAmp: 0.14 });
    return arr;
  },

  aflutter: (st, rate = 75) => {
    const arr = makeArray();
    const period = 0.2; // 300/min flutter waves
    const ratio = clamp(Math.round(300 / rate), 2, 6); // 4:1 → 75/min
    // Lead II sawtooth: slow descent, sharp upstroke, no flat baseline.
    const saw = (ta) => {
      const p = (((ta / period) % 1) + 1) % 1;
      return p < 0.68 ? 0.5 - p / 0.68 : -0.5 + (p - 0.68) / 0.32;
    };
    addFn(arr, (t) => {
      const ta = st.t0 + t;
      // short average rounds the corners the way a real monitor filter does
      return 0.3 * ((saw(ta - 0.01) + saw(ta - 0.005) + saw(ta) + saw(ta + 0.005) + saw(ta + 0.01)) / 5);
    });
    const step = ratio * period;
    const beats = series(
      st,
      "qrs",
      () => ({ t: 0.07 - step * Math.ceil((0.07 + LEAD_IN) / step) }),
      (p) => ({ t: p.t + step })
    );
    addBeats(arr, beats, step, { amp: 0.8, tAmp: 0.06 });
    return arr;
  },

  avb1: (st, rate = 70) => regularRhythm(st, rate, { pr: 0.28 }),

  // Wenckebach 4:3 — PR lengthens by shrinking increments (so the R-R
  // shortens before the pause), then one P wave is not conducted.
  mobitz1: (st, rate = 58.5) => {
    const arr = makeArray();
    const pp = 60 / ((clamp(rate, 40, 62) * 4) / 3);
    const prSteps = [0.16, 0.24, 0.3];
    const ps = steady(st, "p", pp);
    const beats = [];
    for (const p of ps) {
      const pos = p.i % 4;
      if (pos < 3) beats.push({ t: p.t + prSteps[pos] + QRS_ONSET });
      // pos === 3: P wave present, QRS dropped
    }
    addPWaves(arr, ps.map((p) => p.t), 0.15);
    addBeats(arr, beats, pp + 0.07, { amp: 0.95, tAmp: 0.22 });
    return arr;
  },

  // 3:2 block — constant, normal PR on conducted beats; every third P
  // wave is suddenly not followed by a QRS.
  mobitz2: (st, rate = 52) => {
    const arr = makeArray();
    const pp = 60 / (clamp(rate, 35, 60) * 1.5);
    const ps = steady(st, "p", pp);
    const beats = [];
    for (const p of ps) {
      if (p.i % 3 !== 2) beats.push({ t: p.t + 0.16 + QRS_ONSET });
    }
    addPWaves(arr, ps.map((p) => p.t), 0.15);
    addBeats(arr, beats, pp, { amp: 0.95, tAmp: 0.22 });
    return arr;
  },

  // Complete block — P waves march at their own rate straight through a
  // slow, wide ventricular escape rhythm that ignores them.
  avb3: (st, rate = 36) => {
    const arr = makeArray();
    const rr = 60 / clamp(rate, 20, 50);
    addPWaves(arr, steady(st, "p", 60 / 92).map((p) => p.t), 0.14);
    addBeats(arr, steady(st, "qrs", rr), rr, { amp: 0.9, wide: true, tAmp: 0.4 });
    return arr;
  },

  junctional: (st, rate = 48) => regularRhythm(st, rate, { amp: 0.9, tAmp: 0.22 }),

  // Every sinus beat is followed by an early PVC and a full compensatory
  // pause, so each pair spans exactly two sinus cycles.
  pvc_bigeminy: (st, rate = 76) => {
    const arr = makeArray();
    const rrN = 60 / clamp(rate, 50, 100);
    const coupling = clamp(0.6 * rrN, 0.4, 0.52);
    const beats = series(
      st,
      "qrs",
      () => ({ t: Math.random() * rrN - 2 * rrN * Math.ceil((rrN + LEAD_IN) / (2 * rrN)), i: 0 }),
      (p) => ({ t: p.t + (p.i % 2 === 0 ? coupling : 2 * rrN - coupling), i: p.i + 1 })
    ).map((b) => (b.i % 2 === 0 ? { t: b.t, pr: 0.16 } : { t: b.t, wide: true, amp: 1.1, tAmp: 0.45 }));
    addBeats(arr, beats, rrN, {});
    return arr;
  },

  vt: (st, rate = 178) => regularRhythm(st, rate, { amp: 1.1, wide: true, tAmp: 0.5 }),

  // Polymorphic VT: the QRS axis rotates, so in one lead the complexes
  // shrink to a node and re-emerge pointing the other way — spindles.
  torsades: (st, rate = 220) => {
    const arr = makeArray();
    const spin = 2.0; // seconds per half-turn of the axis (one spindle)
    const node = 0.35; // amplitude left at the narrowest point
    // the rotating axis adds half a cycle per spindle; take it off the carrier
    const f = clamp(rate, 170, 260) / 60 - 1 / (2 * spin);
    if (st.spin0 === undefined) st.spin0 = Math.random() * Math.PI;
    const wave = (t) => {
      const ta = st.t0 + t;
      const psi = (Math.PI * ta) / spin + st.spin0;
      const c = Math.cos(psi);
      const s = node * Math.sin(psi);
      const r = Math.hypot(c, s);
      const phi = 2 * Math.PI * f * ta + Math.atan2(s, c);
      // the second harmonic sharpens whichever side the complexes point to
      return 1.05 * r * (Math.sin(phi) - 0.18 * (c / r) * Math.cos(2 * phi));
    };
    addFn(arr, wave);
    for (let i = 0; i < SAMPLE_COUNT; i++) {
      const v = wave(i * DT);
      if (v > 0 && v > wave((i - 1) * DT) && v >= wave((i + 1) * DT)) arr.qrs.push(i * DT);
    }
    return arr;
  },

  vf_coarse: () => fibrillation(1.4, 1.1),

  vf_fine: () => fibrillation(0.42, 1.35, 0.27),

  asystole: () => {
    const arr = makeArray();
    const ph = Math.random() * Math.PI * 2;
    addFn(arr, (t) => (Math.random() - 0.5) * 0.03 + 0.012 * Math.sin(2 * Math.PI * 0.21 * t + ph));
    return arr;
  },
};

// key:      rhythm to draw (unknown keys fall back to NSR)
// rateHint: ventricular rate to draw it at; omit for the rhythm's default
// state:    pass the same object for successive strips to keep them continuous
function synthesizeRhythm(key, rateHint, state) {
  const st = state && typeof state === "object" ? state : {};
  if (st.t0 === undefined) st.t0 = 0;
  const fn = RHYTHMS[key] || RHYTHMS.nsr;
  const arr = Number.isFinite(rateHint) && rateHint > 0 ? fn(st, rateHint) : fn(st);
  st.t0 += STRIP_SECONDS;
  return arr;
}

window.EkgRhythms = { synthesizeRhythm, STRIP_SECONDS, SAMPLE_COUNT };
