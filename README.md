# work-flow
## EKG trainer — running & testing

The app is plain static files (no build step): open `ekg-test.html` from any static server,
e.g. `npm run serve` → http://localhost:4173/ekg-test.html.

Tests are dev-only:

```sh
npm install          # installs @playwright/test only
npm test             # unit (node --test) + e2e (Playwright, desktop + 375px mobile)
npm run test:unit    # pure logic & content/precache checks, no browser
npm run test:e2e     # smoke, mobile layout, offline/PWA, accessibility
```

Playwright uses the system Chromium via `PLAYWRIGHT_BROWSERS_PATH`
(or set `CHROMIUM_PATH=/path/to/chrome`). Don't run `playwright install`.
When adding an `ekg-*.js` module, add it to `CORE_ASSETS` in `service-worker.js` and bump
`CACHE_VERSION` — `tests/unit/service-worker.test.js` enforces this.
