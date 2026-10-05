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

In this dev container Playwright uses the preinstalled Chromium via `PLAYWRIGHT_BROWSERS_PATH`
(or set `CHROMIUM_PATH=/path/to/chrome`). Elsewhere, run `npx playwright install chromium` once.
CI (`.github/workflows/ekg-tests.yml`) runs both suites on every pull request.

**Content is tested automatically.** The unit and e2e suites loop over the data files, so new
content needs no new tests: every rhythm must synthesize a sane waveform, every question and
scenario stage needs distinct choices, an in-range answer and a rationale, every rhythm they
reference must exist (and have a study-guide entry), every generator arc must produce valid cases,
and the e2e suite plays every question and authored scenario through the real UI with the keyed
answer.

**Offline.** Every app file at the repo root (`*.html`, `*.js`, `*.css`, icons, the manifest) must
be listed in `CORE_ASSETS` in `service-worker.js`; `tests/unit/service-worker.test.js` fails
otherwise. Bump `CACHE_VERSION` when you change that list or the page shell.
