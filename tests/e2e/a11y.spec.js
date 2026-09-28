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

test("reduced motion: no looping animations, still monitor strip", async ({ page, errors }) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("ekg-test.html");
  await page.click("#mode-scenario");
  await page.locator("#sc-picker-grid .sc-card").first().click(); // opens on VF: alarm banner + blinking
  const alarm = page.locator("#sc-alarm-banner");
  await expect(alarm).toBeVisible();
  expect(await alarm.evaluate((el) => getComputedStyle(el).animationIterationCount)).toBe("1");

  const snap = () => page.evaluate(() => document.getElementById("sc-ekg-canvas").toDataURL());
  await page.waitForTimeout(300);
  const a = await snap();
  await page.waitForTimeout(500);
  expect(await snap()).toBe(a); // no sweep between frames
});

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
