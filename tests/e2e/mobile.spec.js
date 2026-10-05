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
  // the monitor resizes its backing store on its next animation frame
  await expect
    .poll(() =>
      page.evaluate((cid) => {
        const c = document.getElementById(cid);
        const css = c.clientWidth;
        return css > 0 && css === c.parentElement.clientWidth && c.width === Math.round(css * devicePixelRatio);
      }, id)
    )
    .toBe(true);
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

  await page.click("#mode-adaptive");
  await check(page, "adaptive practice");
  await canvasFits(page, "ad-canvas");
  await page.locator("#ad-choices .softkey").first().click();
  await check(page, "adaptive feedback");
  await page.click("#adaptive-back");

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

test("quiz: question reads before the choices, and feedback scrolls into view", async ({ page, errors }) => {
  await page.emulateMedia({ reducedMotion: "reduce" }); // instant scroll, nothing to wait out
  await page.click("#mode-practice");
  const stem = await page.locator("#q-stem").boundingBox();
  const firstKey = await page.locator("#softkeys .softkey").first().boundingBox();
  expect(stem.y + stem.height).toBeLessThan(firstKey.y);
  expect(stem.y).toBeLessThan(667); // on the first screen

  await page.locator("#softkeys .softkey").first().click();
  await expect(page.locator("#rationale")).toBeInViewport();
  await expect(page.locator("#next-btn")).toBeInViewport();
});

test("header does not stay pinned over content on a phone", async ({ page, errors }) => {
  await page.click("#mode-study");
  await page.evaluate(() => window.scrollTo(0, 600));
  const box = await page.locator(".site-header").boundingBox();
  expect(box.y + box.height).toBeLessThanOrEqual(0);
});
