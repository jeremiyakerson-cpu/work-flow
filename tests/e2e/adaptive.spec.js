// Adaptive code simulator: branching on wrong/late/timed-out decisions,
// difficulty levels, and the upgraded debrief saved to the code log.
const { test, expect } = require("./fixtures");
const { audit } = require("./mobile-audit");

// Click the keyed (or a wrong) choice for the decision on screen.
async function choose(page, wantCorrect) {
  const idx = await page.evaluate((ok) => {
    const st = scState.run.current;
    const target = st.choices[st.answer];
    const keys = [...document.querySelectorAll("#sc-softkeys .softkey")];
    return keys.findIndex((b) => (b.querySelector(".softkey-text").textContent === target) === ok);
  }, wantCorrect);
  expect(idx).toBeGreaterThanOrEqual(0);
  await page.locator("#sc-softkeys .softkey").nth(idx).click();
}

async function startCase(page, id) {
  await page.click("#mode-scenario");
  await page.evaluate((sid) => scStart(EKG_SCENARIOS.find((s) => s.id === sid)), id);
  await expect(page.locator("#sc-shell")).toBeVisible();
}

test.beforeEach(async ({ page }) => {
  await page.goto("ekg-test.html");
  await page.evaluate(() => localStorage.clear());
  await page.reload();
});

test("branching case: a missed shock lets VF persist, the debrief compares against the ideal sequence", async ({ page, errors }) => {
  await startCase(page, "vf-arrest");
  await expect(page.locator("#sc-diff-badge")).toHaveText("STANDARD");
  await choose(page, true); // compressions + code
  await page.click("#sc-next-btn");

  // miss the shock: the answer stays hidden and the patient's state changes
  await choose(page, false);
  await expect(page.locator("#sc-rationale")).toContainText("The answer stays hidden");
  await expect(page.locator("#sc-softkeys .softkey.correct")).toHaveCount(0);
  await expect(page.locator("#sc-next-btn")).toHaveText("See what happens →");
  await page.click("#sc-next-btn");

  await expect(page.locator("#sc-progress")).toHaveText("Decision 2 of 5 · the patient is reacting");
  await expect(page.locator("#sc-narrative")).toContainText("fading toward fine VF");
  expect(await page.evaluate(() => scState.run.current.rhythm)).toBe("vf_fine");
  await expect(page.locator("#sc-alarm-banner")).toContainText("V-FIB");
  await expect(page.locator("#sc-log li")).toHaveCount(2);

  // recover on the re-pose, then run the rest by the book
  await choose(page, true);
  await expect(page.locator("#sc-softkeys .softkey.correct")).toHaveCount(1);
  while (await page.locator("#sc-shell").isVisible()) {
    await page.click("#sc-next-btn");
    if (!(await page.locator("#sc-shell").isVisible())) break;
    await choose(page, true);
  }

  await expect(page.locator("#sc-debrief")).toBeVisible();
  await expect(page.locator("#sc-debrief-score")).toHaveText("4 / 5");
  await expect(page.locator("#sc-debrief-metrics")).toContainText("TIME TO 1ST SHOCK");
  await expect(page.locator("#sc-debrief-metrics")).toContainText("TIME TO EPI");
  const ideal = page.locator("#sc-debrief-ideal li");
  await expect(ideal).toHaveCount(5);
  await expect(ideal.nth(1)).toContainText("Defibrillated 200 J (1st shock)");
  await expect(ideal.nth(1)).toContainText("2nd try");
  await expect(page.locator("#sc-debrief-log li")).toHaveCount(6);
  await expect(page.locator("#sc-debrief-log li").nth(2)).toContainText("2ND TRY");

  const [entry] = await page.evaluate(() => JSON.parse(localStorage.getItem("ekg-code-log-v1")));
  expect(entry.difficulty).toBe("standard");
  expect(entry.metrics.branches).toBe(1);
  expect(entry.metrics.recovered).toBe(1);
  expect(entry.metrics.timeToFirstShockMs).not.toBeNull();
  expect(entry.timeline).toHaveLength(6);
  expect(entry.timeline[1]).toMatchObject({ stage: 1, attempt: 0, correct: false, next: "branch", rhythm: "vf_coarse" });
  expect(entry.misses[0].rhythm).toBe("vf_coarse");

  await page.click("#sc-debrief-log-btn");
  await expect(page.locator(".codelog-entry .codelog-diff")).toHaveText("STANDARD");
  await expect(page.locator(".codelog-entry .codelog-metrics")).toContainText("1st shock");
  await expect(page.locator(".codelog-entry .codelog-metrics")).toContainText("1 branch");
});

test("a delayed shock is graded right but VF persists", async ({ page, errors }) => {
  await page.clock.install();
  await page.reload();
  await startCase(page, "vf-arrest");
  await choose(page, true);
  await page.click("#sc-next-btn");
  await page.clock.runFor(31_000);
  await choose(page, true);
  await expect(page.locator("#sc-rationale")).toContainText("Correct.");
  await expect(page.locator("#sc-outcome")).toContainText("persisted through the delay");
  await page.click("#sc-next-btn");
  await expect(page.locator("#sc-narrative")).toContainText("The shock came late");
  await expect(page.locator("#sc-log li").first()).not.toContainText("LATE");
  await expect(page.locator("#sc-log li").nth(1)).toContainText("LATE");
});

test("expert: hidden rhythm name, decision clock, timeout branches; the choice persists", async ({ page, errors }) => {
  await page.clock.install();
  await page.reload();
  await page.click("#mode-scenario");
  await page.click('#sc-difficulty [data-diff="expert"]');
  await expect(page.locator('#sc-difficulty [data-diff="expert"]')).toHaveAttribute("aria-pressed", "true");
  await expect(page.locator("#sc-diff-note")).toContainText("decision clock");
  await page.evaluate(() => scStart(EKG_SCENARIOS.find((s) => s.id === "vf-arrest")));

  await expect(page.locator("#sc-diff-badge")).toHaveText("EXPERT");
  await expect(page.locator("#sc-alarm-banner")).toHaveText("*** ALARM — CHECK PATIENT ***");
  await expect(page.locator("#sc-hint")).toBeHidden();
  const clock = page.locator("#sc-countdown");
  await expect(clock).toBeVisible();
  await expect(clock).toHaveText("DECIDE 00:20");

  await page.clock.runFor(21_000);
  await expect(page.locator("#sc-rationale")).toContainText("Time ran out.");
  await expect(clock).toBeHidden();
  await page.click("#sc-next-btn");
  await expect(page.locator("#sc-narrative")).toContainText("No decision was made in time.");
  await expect(page.locator("#sc-log li").first()).toContainText("TIMED OUT");

  await page.click("#sc-exit-btn");
  await page.reload();
  await page.click("#mode-scenario");
  await expect(page.locator('#sc-difficulty [data-diff="expert"]')).toHaveAttribute("aria-pressed", "true");
  const [entry] = await page.evaluate(() => JSON.parse(localStorage.getItem("ekg-code-log-v1")));
  expect(entry.outcome).toBe("abandoned");
  expect(entry.metrics.timeouts).toBe(1);
});

test("guided: hints and one wrong option removed", async ({ page, errors }) => {
  await page.click("#mode-scenario");
  await page.click('#sc-difficulty [data-diff="guided"]');
  await page.evaluate(() => scStart(EKG_SCENARIOS.find((s) => s.id === "vf-arrest")));
  await expect(page.locator("#sc-hint")).toBeVisible();
  await expect(page.locator("#sc-hint")).toContainText("time-critical");
  await expect(page.locator("#sc-softkeys .softkey")).toHaveCount(3);
  await expect(page.locator("#sc-countdown")).toBeHidden();
});

test("generated cases target the weakest rhythm in the code log", async ({ page, errors }) => {
  await page.evaluate(() => {
    const run = (r) => Array.from({ length: 3 }, () => ({ rhythm: r, correct: false, attempt: 0, t: 0 }));
    const ok = (r) => Array.from({ length: 3 }, () => ({ rhythm: r, correct: true, attempt: 0, t: 0 }));
    const entries = Array.from({ length: 4 }, () => ({
      title: "seed", outcome: "completed", misses: [], t: 1,
      timeline: [...run("torsades"), ...ok("vf_coarse"), ...ok("svt"), ...ok("sinus_brady"), ...ok("asystole")],
    }));
    localStorage.setItem("ekg-code-log-v1", JSON.stringify(entries));
  });
  const scores = await page.evaluate(() => EkgGenerator.arcScores());
  const top = scores.reduce((a, b) => (b.score > a.score ? b : a));
  expect(top.key).toBe("torsades");
  expect(top.weak).toMatchObject({ rhythm: "torsades", seen: 12, missed: 12 });
  // the generator is weighted-random: the targeted arc turns up quickly
  const reason = await page.evaluate(() => {
    for (let i = 0; i < 40; i++) {
      const s = EkgGenerator.generate();
      if (s.arc === "torsades") return s.reason;
    }
    return null;
  });
  expect(reason).toMatch(/^Targeting a weak rhythm from your code log: you missed 12 of 12 decisions on torsades/);
});

test("debrief fits a 375px phone", async ({ page, errors, isMobile }) => {
  test.skip(!isMobile, "phone-width checks run in the mobile project");
  await startCase(page, "stemi-vt");
  while (await page.locator("#sc-shell").isVisible()) {
    await choose(page, true);
    await page.click("#sc-next-btn");
  }
  await expect(page.locator("#sc-debrief")).toBeVisible();
  const r = await page.evaluate(audit);
  expect(r.overflow).toEqual([]);
  expect(r.scrollWidth).toBeLessThanOrEqual(r.vw);
  expect(r.small).toEqual([]);
  await page.click("#sc-debrief-back");
  const p = await page.evaluate(audit);
  expect(p.small, "difficulty buttons are full-size tap targets").toEqual([]);
});
