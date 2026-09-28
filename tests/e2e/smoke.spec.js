const { test, expect, answerFirst } = require("./fixtures");

test.beforeEach(async ({ page }) => {
  await page.goto("ekg-test.html");
  await page.evaluate(() => localStorage.clear());
  await page.reload();
  await expect(page.locator("#mode-select")).toBeVisible();
});

test("study guide: rhythms, live strip and med cards", async ({ page, errors }) => {
  await page.click("#mode-study");
  await expect(page.locator("#study-screen")).toBeVisible();
  const buttons = page.locator(".study-rhythm-btn");
  expect(await buttons.count()).toBeGreaterThan(5);
  await buttons.nth(3).click();
  await expect(page.locator(".study-rhythm-btn.active")).toHaveText(await buttons.nth(3).textContent());
  await expect(page.locator("#study-criteria .crit-cell")).toHaveCount(5);
  await page.click("#study-tab-meds");
  await expect(page.locator(".med-card").first()).toBeVisible();
  await page.click("#study-back");
  await expect(page.locator("#mode-select")).toBeVisible();
});

test("rhythm sprint: 20 strips to results and a saved best", async ({ page, errors }) => {
  await page.clock.install();
  await page.reload();
  await page.click("#mode-sprint");
  await page.click("#sprint-start");
  let correct = 0;
  for (let i = 1; i <= 20; i++) {
    await expect(page.locator("#sprint-progress")).toHaveText(`Strip ${i} / 20`);
    if (await answerFirst(page, "#sprint-choices")) correct++;
    await page.clock.fastForward(2000); // past the 0.9–1.9 s next-strip delay, without rendering every frame
  }
  await expect(page.locator("#sprint-results")).toBeVisible();
  await expect(page.locator("#sprint-final-score")).toHaveText(`${correct} / 20`);
  expect(await page.evaluate(() => JSON.parse(localStorage.getItem("ekg-sprint-best-v1")).correct)).toBe(correct);
});

test("leaving the sprint mid-strip does not advance a later run", async ({ page, errors }) => {
  await page.clock.install();
  await page.reload();
  await page.click("#mode-sprint");
  await page.click("#sprint-start");
  await answerFirst(page, "#sprint-choices");
  await page.click("#sprint-back"); // before the next-strip delay fires
  await page.click("#mode-sprint");
  await page.click("#sprint-start");
  await page.clock.runFor(3000);
  await expect(page.locator("#sprint-progress")).toHaveText("Strip 1 / 20");
  await expect(page.locator("#sprint-choices .softkey.disabled")).toHaveCount(0);
});

test("practice quiz: full run scores what was shown, resume after reload", async ({ page, errors }) => {
  await page.click("#mode-practice");
  let correct = 0;
  for (let i = 1; i <= 2; i++) {
    if (await answerFirst(page, "#softkeys")) correct++;
    await expect(page.locator("#rationale")).toBeVisible();
    await page.click("#next-btn");
  }
  await page.reload();
  await expect(page.locator("#resume-banner")).toBeVisible();
  await expect(page.locator("#resume-q-num")).toHaveText("3");
  await page.click("#resume-btn");
  await expect(page.locator("#q-num")).toHaveText("3");
  for (let i = 3; i <= 25; i++) {
    // a double tap must not grade the question twice
    const btn = page.locator("#softkeys .softkey").first();
    await btn.click();
    await btn.click({ force: true });
    if ((await btn.getAttribute("class")).match(/\bcorrect\b/)) correct++;
    if (i === 25) await expect(page.locator("#next-btn")).toHaveText("See results →");
    await page.click("#next-btn");
  }
  await expect(page.locator("#results-screen")).toBeVisible();
  await expect(page.locator("#final-score")).toHaveText(`${correct} / 25`);
  expect(await page.evaluate(() => localStorage.getItem("ekg-zoll-trainer-session-v1"))).toBeNull();
});

test("quiz can be exited to the menu and resumed", async ({ page, errors }) => {
  await page.click("#mode-simulation");
  await answerFirst(page, "#softkeys");
  await page.click("#next-btn");
  await page.click("#test-exit-btn");
  await expect(page.locator("#resume-banner")).toBeVisible();
  await expect(page.locator("#resume-q-num")).toHaveText("2");
  await page.click("#resume-btn");
  await expect(page.locator("#q-num")).toHaveText("2");
  await expect(page.locator("#monitor-timer")).toBeVisible();
});

test("comprehensive exam: timer expiry scores unanswered and logs history", async ({ page, errors }) => {
  await page.clock.install();
  await page.reload();
  await page.click("#mode-exam");
  await expect(page.locator("#q-total")).toHaveText("40");
  await expect(page.locator("#monitor-timer")).toHaveText("TIME LEFT 40:00");
  await answerFirst(page, "#softkeys");
  // jump the wall clock: the exam timer must follow real time, not tick counts
  await page.clock.fastForward("40:02");
  await page.clock.runFor(1500);
  await expect(page.locator("#results-screen")).toBeVisible();
  await expect(page.locator("#final-verdict")).toContainText("Time expired with 39 questions unanswered");
  await expect(page.locator("#exam-banner")).toContainText("NOT YET");
  const hist = await page.evaluate(() => JSON.parse(localStorage.getItem("ekg-exam-history-v1")));
  expect(hist).toHaveLength(1);
  expect(hist[0].total).toBe(40);
  await page.click("#retry-btn");
  await page.click("#mode-progress");
  await expect(page.locator("#prog-exams .exam-history-row")).toHaveCount(1);
});

test("code scenario: authored case to debrief, logged once", async ({ page, errors }) => {
  await page.click("#mode-scenario");
  await expect(page.locator("#sc-picker-grid .sc-card").first()).toBeVisible();
  await page.locator("#sc-picker-grid .sc-card").first().click();
  await expect(page.locator("#sc-shell")).toBeVisible();

  // Answer each stage with the correct choice (double-tapping to make sure
  // a stage cannot be graded twice) until the debrief.
  let stages = 0;
  while (await page.locator("#sc-shell").isVisible()) {
    const keys = page.locator("#sc-softkeys .softkey");
    if ((await keys.count()) > 0) {
      stages++;
      await keys.first().click();
      await keys.nth(1).click({ force: true });
      await expect(page.locator("#sc-log li")).toHaveCount(stages);
    }
    await page.click("#sc-next-btn");
  }
  await expect(page.locator("#sc-debrief")).toBeVisible();
  const [score, total] = (await page.locator("#sc-debrief-score").textContent()).split(" / ").map(Number);
  // a lost patient ends the run early, so answered stages can be < total
  expect(stages).toBeLessThanOrEqual(total);
  expect(score).toBeLessThanOrEqual(stages);
  await expect(page.locator("#sc-debrief-log li")).toHaveCount(stages);

  await page.click("#sc-debrief-log-btn");
  await expect(page.locator(".codelog-entry")).toHaveCount(1);
  await page.click("#codelog-back");
  await expect(page.locator("#sc-generated-grid .sc-card-generated")).toHaveCount(1);
});

test("generated case: run, exit early is logged, delete from library", async ({ page, errors }) => {
  await page.click("#mode-scenario");
  await page.click(".sc-generate-card");
  await expect(page.locator("#sc-shell")).toBeVisible();
  await page.locator("#sc-softkeys .softkey").first().click();
  await page.click("#sc-exit-btn");
  await expect(page.locator("#sc-picker")).toBeVisible();
  // the generated case plus the fresh case seeded by the exited run
  const cards = page.locator("#sc-generated-grid .sc-card-generated");
  await expect(cards).toHaveCount(2);
  await page.locator("#sc-generated-grid .sc-card-delete").first().click();
  await expect(cards).toHaveCount(1);
  await expect(page.locator("#sc-picker")).toBeVisible();
  await page.click("#sc-open-log");
  await expect(page.locator(".codelog-entry .codelog-badge")).toHaveText("EXITED EARLY");
});

test("progress dashboard renders and resets", async ({ page, errors }) => {
  await page.click("#mode-progress");
  await expect(page.locator("#prog-tiles .stat-cell")).toHaveCount(4);
  expect(await page.locator("#prog-rhythms .mastery-chip").count()).toBeGreaterThan(5);
  page.once("dialog", (d) => d.accept());
  await page.click("#progress-reset");
  await expect(page.locator("#progress-screen")).toBeVisible();
  await page.click("#progress-back");
  await expect(page.locator("#mode-select")).toBeVisible();
});

test("domain board page loads", async ({ page, errors }) => {
  await page.goto("index.html");
  await expect(page.locator("#domain-grid")).not.toBeEmpty();
});
