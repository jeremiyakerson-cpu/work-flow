const { test, expect } = require("./fixtures");

test("every visible control has an accessible name", async ({ page, errors }) => {
  await page.goto("ekg-test.html");
  const screens = [
    [],
    ["#mode-study"],
    ["#study-back", "#mode-practice"],
    ["#test-exit-btn", "#discard-btn", "#mode-scenario", ".sc-generate-card"],
  ];
  for (const clicks of screens) {
    for (const sel of clicks) await page.click(sel);
    const unnamed = await page.evaluate(() =>
      [...document.querySelectorAll("button, a[href], canvas[role=img]")]
        .filter((el) => el.getClientRects().length)
        .filter((el) => !(el.getAttribute("aria-label") || el.textContent.trim()))
        .map((el) => el.outerHTML.slice(0, 80))
    );
    expect(unnamed).toEqual([]);
  }
});

for (const reducedMotion of ["reduce", "no-preference"]) {
  test(`monitor motion follows prefers-reduced-motion: ${reducedMotion}`, async ({ page, errors }) => {
    // a fake clock drives requestAnimationFrame, so "time passing" is exact
    await page.clock.install();
    await page.emulateMedia({ reducedMotion });
    await page.goto("ekg-test.html");
    await page.click("#mode-scenario");
    await page.locator("#sc-picker-grid .sc-card").first().click(); // opens on VF: alarm banner + blinking
    const alarm = page.locator("#sc-alarm-banner");
    await expect(alarm).toBeVisible();
    const iterations = await alarm.evaluate((el) => getComputedStyle(el).animationIterationCount);

    const snap = () => page.evaluate(() => document.getElementById("sc-ekg-canvas").toDataURL());
    await page.clock.runFor(300);
    const a = await snap();
    await page.clock.runFor(1000); // well inside one 6 s strip
    const b = await snap();
    if (reducedMotion === "reduce") {
      expect(iterations).toBe("1");
      expect(b).toBe(a); // no sweep between frames
    } else {
      expect(iterations).toBe("infinite");
      expect(b).not.toBe(a); // the sweep is running (proves the check above can fail)
    }
  });
}

test("keyboard: answers are reachable and focus moves to Next", async ({ page, errors }) => {
  await page.goto("ekg-test.html");
  await page.focus("#mode-practice");
  await page.keyboard.press("Enter");
  await page.locator("#softkeys .softkey").first().focus();
  await page.keyboard.press("Enter");
  await expect(page.locator("#next-btn")).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page.locator("#q-num")).toHaveText("2");
});
