const test = require("node:test");
const assert = require("node:assert/strict");
const vm = require("vm");
const { load, makeStorage } = require("./load");
const { validateScenario } = require("./validate");

function env(storage = makeStorage()) {
  const ctx = load(["ekg-rhythms.js", "ekg-stats.js", "ekg-generator.js"], { localStorage: storage });
  return { ctx, storage, G: ctx.EkgGenerator, S: ctx.EkgStats, rhythms: Object.keys(vm.runInContext("RHYTHMS", ctx)) };
}

test("generated scenarios are structurally valid across many seeds", () => {
  const { G, rhythms } = env();
  const arcs = new Set();
  for (let i = 0; i < 300; i++) {
    const s = G.generate();
    assert.equal(s.generated, true);
    assert.match(s.id, /^gen-/);
    validateScenario(s, rhythms);
    arcs.add(s.arc);
  }
  assert.ok(arcs.size >= 6, `only saw arcs: ${[...arcs]}`);
});

test("never generates the same arc twice in a row", () => {
  const { G } = env();
  let last = null;
  for (let i = 0; i < 100; i++) {
    const s = G.generate();
    assert.notEqual(s.arc, last);
    last = s.arc;
  }
});

test("library is newest-first, capped at 20, and supports removal", () => {
  const { G } = env();
  const made = [];
  for (let i = 0; i < 25; i++) made.push(G.generate());
  const lib = G.listGenerated();
  assert.equal(lib.length, 20);
  assert.equal(lib[0].id, made[24].id);
  G.removeGenerated(lib[0].id);
  assert.equal(G.listGenerated().length, 19);
  assert.ok(!G.listGenerated().some((s) => s.id === made[24].id));
});

test("weak focus areas are targeted more often", () => {
  const { G, S } = env();
  for (let i = 0; i < 12; i++) S.record({ kind: "scenario", focus: "bradycardia", correct: false });
  for (const f of ["shockable-arrest", "nonshockable-arrest", "tachycardia", "torsades-qt", "stable-vt-acs"]) {
    for (let i = 0; i < 12; i++) S.record({ kind: "scenario", focus: f, correct: true });
  }
  const counts = {};
  for (let i = 0; i < 400; i++) {
    const s = G.generate();
    counts[s.focus] = (counts[s.focus] || 0) + 1;
  }
  const brady = counts.bradycardia || 0;
  const maxOther = Math.max(...Object.entries(counts).filter(([k]) => k !== "bradycardia").map(([, v]) => v));
  assert.ok(brady > maxOther, `bradycardia ${brady} vs max other ${maxOther}: ${JSON.stringify(counts)}`);
  const bradyCase = G.listGenerated().find((s) => s.focus === "bradycardia");
  assert.match(bradyCase.reason, /Targeting a weak area: you've answered 0% correctly in bradycardia/);
});

test("corrupt library storage recovers to empty", () => {
  const { G } = env(makeStorage({ "ekg-generated-scenarios-v1": "oops" }));
  assert.deepEqual([...G.listGenerated()], []);
  G.generate();
  assert.equal(G.listGenerated().length, 1);
});
