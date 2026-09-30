// Installs the service worker, goes offline, and checks the app still
// launches and runs from the precache.
const { test, expect } = require("./fixtures");

test.skip(({ isMobile }) => isMobile, "offline check runs once, in the desktop project");

test("app works offline after the first visit", async ({ page, context, errors }) => {
  await page.goto("ekg-test.html");
  await page.evaluate(async () => {
    await navigator.serviceWorker.ready;
  });
  // the page's first load isn't controlled yet — reload under the worker
  await page.reload();
  await expect.poll(() => page.evaluate(() => !!navigator.serviceWorker.controller)).toBe(true);

  const cacheInfo = await page.evaluate(async () => {
    const keys = await caches.keys();
    const cache = await caches.open(keys.find((k) => k.startsWith("ekg-trainer-v")));
    return { keys, urls: (await cache.keys()).map((r) => new URL(r.url).pathname) };
  });
  expect(cacheInfo.keys.filter((k) => k.startsWith("ekg-trainer-"))).toHaveLength(1);
  expect(cacheInfo.urls).toContain("/ekg-generator.js");

  await context.setOffline(true);
  await page.reload();
  await expect(page.locator("#mode-select")).toBeVisible();
  expect(await page.evaluate(() => typeof EKG_QUESTIONS !== "undefined" && typeof EkgGenerator !== "undefined")).toBe(true);

  await page.click("#mode-study");
  await expect(page.locator(".study-rhythm-btn").first()).toBeVisible();
  await page.click("#study-back");
  await page.click("#mode-scenario");
  await page.click(".sc-generate-card");
  await expect(page.locator("#sc-softkeys .softkey").first()).toBeVisible();

  // a query string must still hit the cache, and the hub page is available too
  await page.goto("ekg-test.html?source=pwa");
  await expect(page.locator("#mode-select")).toBeVisible();
  await page.goto("index.html");
  await expect(page.locator("#domain-grid")).not.toBeEmpty();
  await context.setOffline(false);
});

test("old versioned caches are removed on activate", async ({ page, errors }) => {
  await page.goto("ekg-test.html");
  await page.evaluate(async () => {
    await caches.open("ekg-trainer-v1");
    await caches.open("unrelated-cache");
  });
  await page.evaluate(async () => {
    const reg = await navigator.serviceWorker.ready;
    // simulate a new deploy: unregister + re-register runs install/activate again
    await reg.unregister();
    await navigator.serviceWorker.register("service-worker.js");
    await navigator.serviceWorker.ready;
  });
  await expect
    .poll(() => page.evaluate(() => caches.keys()), { timeout: 10_000 })
    .not.toContain("ekg-trainer-v1");
  const keys = await page.evaluate(() => caches.keys());
  expect(keys).toContain("unrelated-cache");
});
