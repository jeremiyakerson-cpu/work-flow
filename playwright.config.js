// Dev-only e2e config. Uses the system Chromium (PLAYWRIGHT_BROWSERS_PATH,
// or CHROMIUM_PATH to point at a specific binary) — no browser download.
const { defineConfig } = require("@playwright/test");

const PORT = Number(process.env.PORT || 4173);
const launchOptions = process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {};

module.exports = defineConfig({
  testDir: "tests/e2e",
  timeout: 90_000,
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: 0, // tests must be deterministic; a flaky test is a bug to fix, not retry
  reporter: process.env.CI ? [["line"], ["html", { open: "never" }]] : "list",
  use: {
    baseURL: `http://localhost:${PORT}/`,
    browserName: "chromium",
    launchOptions,
  },
  projects: [
    { name: "desktop", use: { viewport: { width: 1280, height: 800 } } },
    { name: "mobile", use: { viewport: { width: 375, height: 667 }, isMobile: true, hasTouch: true, deviceScaleFactor: 2 } },
  ],
  webServer: {
    command: `node tests/serve.js ${PORT}`,
    url: `http://localhost:${PORT}/ekg-test.html`,
    reuseExistingServer: !process.env.CI,
  },
});
