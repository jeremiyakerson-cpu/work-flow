const assert = require("node:assert/strict");

const LOC = ["ALERT", "ALTERED", "UNRESPONSIVE"];
const PULSE = ["PRESENT", "WEAK", "ABSENT"];
const RESP = ["SPONTANEOUS", "ASSISTED", "NONE"];

// Choice text is rendered with innerHTML, so tag-like text would be parsed
// as markup. ("<60" is fine: a tag must start with a letter, / or !.)
const hasMarkup = (s) => /<[a-z/!]/i.test(s);

// Choices are shuffled before display, so an answer is only unambiguous if
// the choices are distinct and none of them points at the others.
const RELATIVE_CHOICE = /\b(all|none|both|neither) of the (above|following|choices)\b|\b[a-d] and [a-d]\b/i;

function validateChoices(choices, answer) {
  assert.ok(Array.isArray(choices) && choices.length >= 2, "needs at least 2 choices");
  for (const c of choices) assert.ok(typeof c === "string" && c.trim(), "empty choice");
  const norm = choices.map((c) => c.trim().toLowerCase().replace(/\s+/g, " "));
  assert.equal(new Set(norm).size, choices.length, "duplicate choices (two could be 'correct')");
  for (const c of choices) assert.ok(!RELATIVE_CHOICE.test(c), `choice depends on order, which is shuffled: "${c}"`);
  assert.ok(Number.isInteger(answer) && answer >= 0 && answer < choices.length, `answer index ${answer} out of range`);
}

function validateVitals(v) {
  assert.ok(v && typeof v === "object", "vitals");
  for (const k of ["hr", "spo2", "nibp", "rr"]) assert.ok(k in v, `vitals.${k}`);
  for (const k of ["hr", "spo2", "rr", "etco2"]) {
    if (v[k] != null) assert.ok(Number.isFinite(v[k]) && v[k] >= 0, `vitals.${k} = ${v[k]}`);
  }
  assert.equal(typeof v.nibp, "string", "vitals.nibp");
}

function validateScenario(s, rhythmKeys) {
  const where = `scenario ${s.id}`;
  assert.ok(s.id && s.title && s.blurb, `${where}: id/title/blurb`);
  assert.ok(Array.isArray(s.stages) && s.stages.length > 0, `${where}: stages`);
  s.stages.forEach((st, i) => {
    const w = `${where} stage ${i + 1}`;
    assert.ok(rhythmKeys.includes(st.rhythm), `${w}: unknown rhythm ${st.rhythm}`);
    validateVitals(st.vitals);
    assert.ok(st.scene, `${w}: scene`);
    assert.ok(LOC.includes(st.scene.loc), `${w}: loc ${st.scene.loc}`);
    assert.ok(PULSE.includes(st.scene.pulse), `${w}: pulse ${st.scene.pulse}`);
    assert.ok(RESP.includes(st.scene.breathing), `${w}: breathing ${st.scene.breathing}`);
    for (const f of ["narrative", "question", "rationale", "outcome", "intervention"]) {
      assert.ok(typeof st[f] === "string" && st[f].trim(), `${w}: ${f}`);
    }
    try {
      validateChoices(st.choices, st.answer);
    } catch (e) {
      e.message = `${w}: ${e.message}`;
      throw e;
    }
    for (const text of [st.question, ...st.choices]) assert.ok(!hasMarkup(text), `${w}: HTML-like text: ${text}`);
  });
}

module.exports = { validateScenario, validateChoices, validateVitals, hasMarkup };
