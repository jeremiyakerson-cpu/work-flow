const assert = require("node:assert/strict");

const LOC = ["ALERT", "ALTERED", "UNRESPONSIVE"];
const PULSE = ["PRESENT", "WEAK", "ABSENT"];
const RESP = ["SPONTANEOUS", "ASSISTED", "NONE"];

function validateScenario(s, rhythmKeys) {
  const where = `scenario ${s.id}`;
  assert.ok(s.id && s.title && s.blurb, `${where}: id/title/blurb`);
  assert.ok(Array.isArray(s.stages) && s.stages.length > 0, `${where}: stages`);
  s.stages.forEach((st, i) => {
    const w = `${where} stage ${i + 1}`;
    assert.ok(rhythmKeys.includes(st.rhythm), `${w}: unknown rhythm ${st.rhythm}`);
    assert.ok(st.vitals && typeof st.vitals === "object", `${w}: vitals`);
    assert.ok(st.scene, `${w}: scene`);
    assert.ok(LOC.includes(st.scene.loc), `${w}: loc ${st.scene.loc}`);
    assert.ok(PULSE.includes(st.scene.pulse), `${w}: pulse ${st.scene.pulse}`);
    assert.ok(RESP.includes(st.scene.breathing), `${w}: breathing ${st.scene.breathing}`);
    assert.ok(st.narrative && st.question && st.rationale && st.outcome && st.intervention, `${w}: text fields`);
    assert.ok(Array.isArray(st.choices) && st.choices.length >= 2, `${w}: choices`);
    assert.equal(new Set(st.choices).size, st.choices.length, `${w}: duplicate choices`);
    assert.ok(Number.isInteger(st.answer) && st.answer >= 0 && st.answer < st.choices.length, `${w}: answer index`);
  });
}

module.exports = { validateScenario };
