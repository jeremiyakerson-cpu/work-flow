const { test, expect, answerFirst } = require("./fixtures");

// A learner with existing (v1) history: afib missed repeatedly two days ago.
async function seedLegacy(page) {
  await page.evaluate(() => {
    localStorage.clear();
    const t = Date.now() - 2 * 24 * 3600 * 1000;
    const events = [];
    for (let i = 0; i < 4; i++) events.push({ kind: "quiz", category: "rhythm", rhythm: "afib", focus: null, correct: false, t: t + i });
    for (let i = 0; i < 6; i++) events.push({ kind: "sprint", category: "rhythm", rhythm: "nsr", focus: null, correct: true, t: t + 10 + i });
    localStorage.setItem("ekg-zoll-trainer-stats-v1", JSON.stringify({ events }));
  });
  await page.reload();
}

test.beforeEach(async ({ page }) => {
  await page.goto("ekg-test.html");
  await seedLegacy(page);
  await expect(page.locator("#mode-select")).toBeVisible();
});

test("existing history is migrated and drives the home study plan", async ({ page, errors }) => {
  const store = await page.evaluate(() => JSON.parse(localStorage.getItem("ekg-mastery-v2")));
  expect(store.version).toBe(2);
  expect(store.migratedFrom).toBe("ekg-zoll-trainer-stats-v1");
  expect(store.rhythms.afib.n).toBe(4);
  expect(store.rhythms.nsr.n).toBe(6);

  const card = page.locator("#next-card");
  await expect(card).toBeVisible();
  await expect(page.locator("#next-steps li").first()).toContainText("Atrial fibrillation");
  await expect(page.locator("#next-goal-count")).toHaveText("0 / 15");
  const segs = page.locator("#next-mastery .mastery-seg");
  expect(await segs.count()).toBeGreaterThan(20);
  await expect(page.locator("#next-mastery .mastery-seg-bad")).toHaveCount(1);
  await expect(page.locator("#next-mastery")).toHaveAttribute("aria-label", /1 weak/);

  await expect(page.locator("#next-study")).toHaveText(/Study Atrial fibrillation/);
  await page.click("#next-study");
  await expect(page.locator("#study-screen")).toBeVisible();
  await expect(page.locator("#study-rhythm-name")).toHaveText("Atrial fibrillation");
});

test("adaptive practice: weak spot first, a full round, mastery and schedule updated", async ({ page, errors }) => {
  await page.click("#mode-adaptive");
  await expect(page.locator("#adaptive-screen")).toBeVisible();
  await expect(page.locator("#ad-progress")).toHaveText("Item 1 / 15");
  await expect(page.locator("#ad-reason")).toHaveText("WEAK SPOT");
  await expect(page.locator("#ad-category")).toContainText("LEVEL 1");
  await expect(page.locator("#ad-choices .softkey")).toHaveCount(4);

  let correct = 0;
  for (let i = 1; i <= 15; i++) {
    await expect(page.locator("#ad-progress")).toHaveText(`Item ${i} / 15`);
    const btn = page.locator("#ad-choices .softkey").first();
    if (await answerFirst(page, "#ad-choices")) correct++;
    await btn.click({ force: true }); // a double tap must not grade twice
    await expect(page.locator("#ad-choices .softkey.correct")).toHaveCount(1);
    await expect(page.locator("#ad-feedback")).toBeVisible();
    await expect(page.locator("#ad-delta")).toContainText("mastery");
    await page.click("#ad-next");
  }
  await expect(page.locator("#ad-results")).toBeVisible();
  await expect(page.locator("#ad-final-score")).toHaveText(`${correct} / 15`);
  expect(await page.locator("#ad-changes .mastery-chip").count()).toBeGreaterThan(0);

  const store = await page.evaluate(() => JSON.parse(localStorage.getItem("ekg-mastery-v2")));
  expect(Object.keys(store.items)).toHaveLength(15);
  expect(store.rhythms.afib.n).toBeGreaterThan(4);
  // every answer also lands in the v1 log the scenario generator reads
  const legacy = await page.evaluate(() => JSON.parse(localStorage.getItem("ekg-zoll-trainer-stats-v1")).events);
  expect(legacy.filter((e) => e.kind === "adaptive")).toHaveLength(15);

  await page.click("#ad-results-back");
  await expect(page.locator("#next-goal-count")).toHaveText("15 / 15");
  // another round is planned around what was just answered
  await page.click("#mode-adaptive");
  await expect(page.locator("#ad-progress")).toHaveText("Item 1 / 15");
});

test("missed items come back as due reviews", async ({ page, errors }) => {
  await page.click("#mode-adaptive");
  // miss the first item on purpose
  const wrong = page.locator("#ad-choices .softkey:not([data-correct])").first();
  await wrong.click();
  await expect(wrong).toHaveClass(/incorrect/);
  await expect(page.locator("#ad-delta")).toContainText("next review in 10 min");
  await page.click("#adaptive-back");
  // let the 10-minute relearning step pass (move the stored due time back)
  await page.evaluate(() => {
    const s = JSON.parse(localStorage.getItem("ekg-mastery-v2"));
    for (const c of Object.values(s.items)) c.due -= 11 * 60 * 1000;
    localStorage.setItem("ekg-mastery-v2", JSON.stringify(s));
  });
  // the card refreshes whenever the menu is shown again
  await page.click("#mode-progress");
  await page.click("#progress-back");
  await expect(page.locator("#next-steps li").first()).toHaveText("Review 1 item due for spaced repetition");
  await page.click("#mode-adaptive");
  await expect(page.locator("#ad-reason")).toHaveText("DUE REVIEW");
});

test("progress dashboard shows model mastery and reset clears it", async ({ page, errors }) => {
  await page.click("#mode-progress");
  const afib = page.locator("#prog-rhythms .mastery-chip", { hasText: "Atrial fibrillation" }).first();
  await expect(afib).toHaveClass(/mastery-bad/);
  await expect(afib.locator(".mastery-detail")).toContainText("4 answers");
  page.once("dialog", (d) => d.accept());
  await page.click("#progress-reset");
  const store = await page.evaluate(() => JSON.parse(localStorage.getItem("ekg-mastery-v2")));
  expect(store.rhythms).toEqual({});
  await page.click("#progress-back");
  await expect(page.locator("#next-mastery .mastery-seg-bad")).toHaveCount(0);
});
