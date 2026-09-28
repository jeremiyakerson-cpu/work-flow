// Every screen at phone width: no horizontal scrolling, tap targets of at
// least 44px, and live monitor canvases that fit their column (including
// after rotating to landscape).
const { test, expect } = require("./fixtures");
const { audit } = require("./mobile-audit");

test.skip(({ isMobile }) => !isMobile, "phone-width checks run in the mobile project");

async function check(page, name) {
  const r = await page.evaluate(audit);
  expect(r.overflow, `${name}: elements past the right edge`).toEqual([]);
  expect(r.scrollWidth, `${name}: page scrolls sideways`).toBeLessThanOrEqual(r.vw);
  expect(r.small, `${name}: tap targets under 44px`).toEqual([]);
}

async function canvasFits(page, id) {
  // give the monitor a frame to resize its backing store
  await page.waitForTimeout(100);
  const m = await page.evaluate((cid) => {
    const c = document.getElementById(cid);
    return { css: c.clientWidth, parent: c.parentElement.clientWidth, backing: c.width, dpr: devicePixelRatio };
  }, id);
  expect(m.css).toBe(m.parent);
  expect(m.backing).toBe(Math.round(m.css * m.dpr));
}

test.beforeEach(async ({ page }) => {
  await page.goto("ekg-test.html");
  await page.evaluate(() => localStorage.clear());
  await page.reload();
});

test("every screen fits a 375px phone", async ({ page, errors }) => {
  await check(page, "menu");

  await page.click("#mode-study");
  await check(page, "study");
  await canvasFits(page, "study-canvas");
  await page.click("#study-tab-meds");
  await check(page, "meds");
  await page.click("#study-back");

  await page.click("#mode-sprint");
  await check(page, "sprint intro");
  await page.click("#sprint-start");
  await check(page, "sprint run");
  await canvasFits(page, "sprint-canvas");
  await page.click("#sprint-back");

  await page.click("#mode-practice");
  await page.locator("#softkeys .softkey").first().click();
  await check(page, "quiz");
  await canvasFits(page, "ekg-canvas");
  await page.click("#test-exit-btn");
  await check(page, "resume banner");
  await page.click("#discard-btn");

  await page.click("#mode-scenario");
  await page.click(".sc-generate-card");
  await page.locator("#sc-softkeys .softkey").first().click();
  await check(page, "scenario");
  await canvasFits(page, "sc-ekg-canvas");
  await page.click("#sc-exit-btn");
  await check(page, "picker");
  await page.click("#sc-open-log");
  await check(page, "code log");
  await page.click("#codelog-back");
  await page.click("#sc-picker-back");

  await page.click("#mode-progress");
  await check(page, "progress");

  await page.goto("index.html");
  await check(page, "domain board");
});

test("monitor rescales on rotation", async ({ page, errors }) => {
  await page.click("#mode-practice");
  await canvasFits(page, "ekg-canvas");
  await page.setViewportSize({ width: 667, height: 375 });
  await canvasFits(page, "ekg-canvas");
  const r = await page.evaluate(audit);
  expect(r.scrollWidth).toBeLessThanOrEqual(r.vw);
  await page.setViewportSize({ width: 375, height: 667 });
  await canvasFits(page, "ekg-canvas");
});
