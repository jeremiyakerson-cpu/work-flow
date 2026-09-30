// Data-driven: plays every question and every authored scenario through
// the real UI, so new content is exercised end to end automatically.
// Runs once (desktop); layout at phone width is covered by mobile.spec.js.
const { test, expect } = require("./fixtures");

test.skip(({ isMobile }) => isMobile, "content walk-through runs once, in the desktop project");

test.beforeEach(async ({ page }) => {
  await page.goto("ekg-test.html");
  await page.evaluate(() => localStorage.clear());
  await page.reload();
});

test("every question renders and its keyed answer is graded correct", async ({ page, errors }) => {
  await page.click("#mode-practice");
  const problems = await page.evaluate(() => {
    const out = [];
    const RHYTHM_WORDS = /\b(v-?fib|v-?tach|asystole|torsades|fibrillation|flutter|block|sinus)\b/i;
    EKG_QUESTIONS.forEach((q, i) => {
      const where = `question ${q.id}`;
      // show bank question i at the current slot, unanswered
      state.order[state.current] = i;
      state.answered[state.current] = null;
      renderQuestion();
      const keys = [...document.querySelectorAll("#softkeys .softkey")];
      if (keys.length !== q.choices.length) out.push(`${where}: ${keys.length} buttons for ${q.choices.length} choices`);
      const right = keys.filter((b) => b.querySelector(".softkey-text").textContent === q.choices[q.answer]);
      if (right.length !== 1) {
        out.push(`${where}: keyed answer shown ${right.length} times (HTML in the text?)`);
        return;
      }
      if (q.category === "rhythm") {
        const alarm = document.getElementById("alarm-banner");
        if (!alarm.hidden && RHYTHM_WORDS.test(alarm.textContent)) out.push(`${where}: alarm gives the rhythm away: ${alarm.textContent}`);
        const label = document.getElementById("ekg-canvas").getAttribute("aria-label") || "";
        if (label.toLowerCase().includes(q.choices[q.answer].toLowerCase())) out.push(`${where}: canvas label names the answer`);
      }
      right[0].click();
      if (!right[0].classList.contains("correct")) out.push(`${where}: keyed answer not marked correct`);
      if (document.getElementById("rationale").hidden) out.push(`${where}: no rationale shown`);
    });
    return out;
  });
  expect(problems).toEqual([]);
});

test("every authored scenario can be completed with its keyed answers for a perfect score", async ({ page, errors }) => {
  await page.click("#mode-scenario");
  const results = await page.evaluate(() =>
    EKG_SCENARIOS.map((s) => {
      scStart(s);
      const problems = [];
      let guard = 0;
      while (!document.getElementById("sc-shell").hidden && guard++ < 50) {
        const st = s.stages[scState.stageIdx];
        const keys = [...document.querySelectorAll("#sc-softkeys .softkey")];
        const right = keys.filter((b) => b.querySelector(".softkey-text").textContent === st.choices[st.answer]);
        if (right.length !== 1) {
          problems.push(`stage ${scState.stageIdx + 1}: keyed answer shown ${right.length} times`);
          break;
        }
        right[0].click();
        if (!right[0].classList.contains("correct")) problems.push(`stage ${scState.stageIdx + 1}: keyed answer not marked correct`);
        document.getElementById("sc-next-btn").click();
      }
      const score = document.getElementById("sc-debrief-score").textContent;
      if (score !== `${s.stages.length} / ${s.stages.length}`) problems.push(`debrief score ${score}`);
      return { id: s.id, problems };
    })
  );
  expect(results.filter((r) => r.problems.length)).toEqual([]);
  expect(results.length).toBeGreaterThan(0);
});
