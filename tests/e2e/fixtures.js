// Shared fixture: every test fails if the page logs a console error or
// throws an uncaught exception.
const base = require("@playwright/test");

const test = base.test.extend({
  errors: async ({ page }, use) => {
    const errors = [];
    page.on("console", (m) => {
      if (m.type() === "error") errors.push(`console: ${m.text()}`);
    });
    page.on("pageerror", (e) => errors.push(`pageerror: ${e.message}`));
    await use(errors);
    base.expect(errors, errors.join("\n")).toEqual([]);
  },
});

// Clicks the first answer button in a softkey container and returns
// whether it was marked correct.
async function answerFirst(page, containerSel) {
  const btn = page.locator(`${containerSel} .softkey`).first();
  await btn.click();
  return (await btn.getAttribute("class")).includes("correct") && !(await btn.getAttribute("class")).includes("incorrect");
}

module.exports = { test, expect: base.expect, answerFirst };
