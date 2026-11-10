// Guards the textbook properties of the drawn rhythms: the rate the monitor
// measures must equal the rate actually on the strip, strips must join
// without a pause, and the defining features (P waves, PR, QRS width,
// dropped beats) must be where a learner would measure them.
const test = require("node:test");
const assert = require("node:assert/strict");
const { load } = require("./load");

const DUR = 6;

function fresh(seed) {
  return load(["ekg-rhythms.js", "ekg-monitor.js"], { seed });
}

// Two successive strips joined into one 12 s trace, with the generator's
// own list of QRS times, as the monitor sees them.
function twoStrips(ctx, key, rate) {
  const state = {};
  const a = ctx.EkgRhythms.synthesizeRhythm(key, rate, state);
  const b = ctx.EkgRhythms.synthesizeRhythm(key, rate, state);
  const joined = new Float32Array(a.length * 2);
  joined.set(a);
  joined.set(b, a.length);
  const truth = [...a.qrs, ...b.qrs.map((t) => t + DUR)];
  return { a, b, joined, truth, perSec: a.length / DUR };
}

const at = (strip, t) => strip[Math.round((t / DUR) * strip.length)];

// rhythm → the ventricular rate its default strip should show
const DEFAULT_RATE = {
  nsr: 76,
  sinus_brady: 44,
  sinus_tach: 122,
  svt: 186,
  aflutter: 75,
  avb1: 70,
  mobitz1: 58.5,
  mobitz2: 52,
  avb3: 36,
  junctional: 48,
  pvc_bigeminy: 76,
  vt: 178,
  torsades: 220,
};

test("the monitor finds exactly the QRS complexes that were drawn", async (t) => {
  for (const key of [...Object.keys(DEFAULT_RATE), "afib"]) {
    await t.test(key, () => {
      for (let seed = 1; seed <= 12; seed++) {
        const ctx = fresh(seed);
        const { joined, truth, perSec } = twoStrips(ctx, key);
        const found = ctx.EkgMonitor.detectBeats(joined, perSec);
        // complexes cut by the ends of the trace can't be judged either way
        const inner = (list) => list.filter((x) => x > 0.1 && x < 2 * DUR - 0.1);
        const near = (list, x) => list.some((y) => Math.abs(x - y) < 0.02);
        for (const x of inner(truth)) assert.ok(near(found, x), `seed ${seed}: missed the beat drawn at ${x.toFixed(3)} s`);
        for (const x of inner(found)) assert.ok(near(truth, x), `seed ${seed}: counted a beat at ${x.toFixed(3)} s that was not drawn`);
      }
    });
  }
});

test("the measured rate matches the rate of the drawn rhythm", async (t) => {
  for (const [key, want] of Object.entries(DEFAULT_RATE)) {
    await t.test(key, () => {
      for (let seed = 1; seed <= 12; seed++) {
        const ctx = fresh(seed);
        const { joined, perSec } = twoStrips(ctx, key);
        const rate = ctx.EkgMonitor.measuredRate(ctx.EkgMonitor.detectBeats(joined, perSec));
        // torsades slips a beat as each spindle turns over
        const slack = key === "torsades" ? 6 : 1.5;
        assert.ok(Math.abs(rate - want) <= slack, `seed ${seed}: measured ${rate}, drawn at ${want}`);
      }
    });
  }
  await t.test("afib averages its ventricular rate", () => {
    let sum = 0;
    for (let seed = 1; seed <= 40; seed++) {
      const ctx = fresh(seed);
      const { joined, perSec } = twoStrips(ctx, "afib", 140);
      sum += ctx.EkgMonitor.measuredRate(ctx.EkgMonitor.detectBeats(joined, perSec));
    }
    assert.ok(Math.abs(sum / 40 - 140) < 5, `mean measured ${sum / 40}`);
  });
});

test("a charted rate is honoured by every rhythm that has one", () => {
  const cases = [
    ["nsr", 96],
    ["sinus_brady", 38],
    ["sinus_tach", 132],
    ["svt", 190],
    ["avb1", 64],
    ["avb3", 32],
    ["junctional", 70],
    ["vt", 172],
    ["torsades", 210],
    ["pvc_bigeminy", 84],
  ];
  for (const [key, want] of cases) {
    const ctx = fresh(3);
    const { joined, perSec } = twoStrips(ctx, key, want);
    const rate = ctx.EkgMonitor.measuredRate(ctx.EkgMonitor.detectBeats(joined, perSec));
    assert.ok(Math.abs(rate - want) <= (key === "torsades" ? 6 : 1.5), `${key}: measured ${rate}, charted ${want}`);
  }
});

test("successive strips join without a pause or an extra beat", async (t) => {
  for (const key of ["nsr", "sinus_brady", "svt", "avb1", "junctional", "vt"]) {
    await t.test(key, () => {
      const ctx = fresh(5);
      const { truth } = twoStrips(ctx, key);
      const rr = 60 / DEFAULT_RATE[key];
      for (let i = 1; i < truth.length; i++) {
        assert.ok(Math.abs(truth[i] - truth[i - 1] - rr) < 1e-6, `gap ${truth[i] - truth[i - 1]} at ${truth[i]}`);
      }
      // and the strip is full edge to edge
      assert.ok(truth[0] < rr && truth[truth.length - 1] > 2 * DUR - rr);
    });
  }
});

test("irregular rhythms are irregular in the right way", async (t) => {
  const gaps = (list) => list.slice(1).map((x, i) => x - list[i]);
  await t.test("afib: irregularly irregular", () => {
    const g = gaps(twoStrips(fresh(2), "afib").truth);
    assert.ok(Math.max(...g) / Math.min(...g) > 1.3);
    assert.ok(new Set(g.map((x) => x.toFixed(2))).size > g.length / 2, "R-R intervals repeat");
  });
  await t.test("mobitz I: R-R shortens, then a pause shorter than two cycles", () => {
    const g = gaps(twoStrips(fresh(2), "mobitz1").truth);
    const pp = 60 / 78;
    const pause = g.findIndex((x) => x > 1.2 * pp);
    assert.ok(pause >= 0 && pause + 2 < g.length);
    assert.ok(g[pause] < 2 * pp, "Wenckebach pause must be less than 2 x P-P");
    assert.ok(g[pause + 1] > g[pause + 2], "R-R should shorten through the cycle");
  });
  await t.test("mobitz II: pauses are exactly two P-P cycles", () => {
    const g = gaps(twoStrips(fresh(2), "mobitz2").truth);
    const pp = 60 / 78;
    for (const x of g) assert.ok(Math.abs(x - pp) < 1e-6 || Math.abs(x - 2 * pp) < 1e-6, `gap ${x}`);
    assert.ok(g.some((x) => x > 1.5 * pp));
  });
  await t.test("bigeminy: early beat, full compensatory pause", () => {
    const g = gaps(twoStrips(fresh(2), "pvc_bigeminy").truth);
    for (let i = 1; i < g.length; i++) assert.ok(Math.abs(g[i] + g[i - 1] - 2 * (60 / 76)) < 1e-6);
    assert.ok(Math.min(...g) < 0.55 && Math.max(...g) > 1);
  });
});

test("P waves and PR intervals sit where a learner measures them", () => {
  const QRS_ONSET = 0.04;
  const P_HALF = 0.045;
  // height of the trace where a P wave with this PR would peak, for a mid-strip beat
  const pPeak = (key, pr) => {
    const { a } = twoStrips(fresh(4), key);
    const r = a.qrs.find((x) => x > 1.5 && x < 4.5);
    return at(a, r - QRS_ONSET - pr + P_HALF);
  };
  assert.ok(pPeak("nsr", 0.16) > 0.12, "NSR: P wave with a 0.16 s PR");
  assert.ok(pPeak("sinus_tach", 0.14) > 0.12, "sinus tach: P wave stays clear of the T wave");
  assert.ok(pPeak("avb1", 0.28) > 0.12, "first-degree block: P wave with a 0.28 s PR");
  assert.ok(Math.abs(pPeak("avb1", 0.16)) < 0.03, "first-degree block: nothing at a normal PR");
  assert.ok(Math.abs(pPeak("junctional", 0.16)) < 0.03, "junctional: no P wave");
  assert.ok(Math.abs(pPeak("svt", 0.1)) < 0.05, "SVT: no P wave before the QRS");
});

test("ventricular complexes are wide with an opposite T wave", () => {
  const halfWidth = (key) => {
    const { a } = twoStrips(fresh(4), key);
    const r = a.qrs.find((x) => x > 1.5 && x < 4.5);
    const peak = at(a, r);
    let w = 0;
    for (let d = -0.1; d <= 0.1; d += 0.005) if (at(a, r + d) > peak / 2) w += 0.005;
    return w;
  };
  assert.ok(halfWidth("nsr") < 0.045, `narrow QRS half-width ${halfWidth("nsr")}`);
  assert.ok(halfWidth("vt") > 1.6 * halfWidth("nsr"), "VT must read wide next to a sinus beat");
  const { a } = twoStrips(fresh(4), "avb3");
  const r = a.qrs.find((x) => x > 1 && x < 4.5);
  let low = 0;
  for (let d = 0.15; d < 0.5; d += 0.005) low = Math.min(low, at(a, r + d));
  assert.ok(low < -0.2, "escape beat should carry a discordant (inverted) T wave");
});

test("arrest rhythms have nothing a monitor could count", () => {
  for (const key of ["vf_fine", "asystole"]) {
    for (let seed = 1; seed <= 10; seed++) {
      const ctx = fresh(seed);
      const { joined, perSec } = twoStrips(ctx, key);
      assert.equal(ctx.EkgMonitor.detectBeats(joined, perSec).length, 0, `${key} seed ${seed}`);
    }
  }
});

test("the pleth pulses once per beat and goes flat without beats", () => {
  const ctx = fresh(1);
  assert.ok(ctx.EkgMonitor.makePleth([], 1200).every((v) => v === 0));
  const times = [0.5, 1.3, 2.1, 2.9, 3.7, 4.5, 5.3];
  const p = ctx.EkgMonitor.makePleth(times, 1200);
  let pulses = 0;
  for (let i = 1; i < p.length; i++) if (p[i - 1] < 0.6 && p[i] >= 0.6) pulses++;
  assert.equal(pulses, times.length);
  for (const v of p) assert.ok(v >= 0 && v <= 1.1);
});
