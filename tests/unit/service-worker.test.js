// The app must work offline, so every file a page loads has to be in the
// service worker's precache list — including ekg-*.js modules added later.
const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("fs");
const path = require("path");
const vm = require("vm");
const { ROOT } = require("./load");

function swConfig() {
  const ctx = { self: { addEventListener() {} }, caches: {}, URL, Request: class {}, Response: class {} };
  vm.createContext(ctx);
  vm.runInContext(fs.readFileSync(path.join(ROOT, "service-worker.js"), "utf8") + ";this.__cfg={CORE_ASSETS,CACHE_NAME};", ctx);
  return ctx.__cfg;
}

function referencedAssets(htmlFile) {
  const html = fs.readFileSync(path.join(ROOT, htmlFile), "utf8");
  const refs = [...html.matchAll(/<(?:script|link)\b[^>]*\b(?:src|href)="([^"]+)"/g)].map((m) => m[1]);
  return refs.filter((r) => !/^(https?:)?\/\//.test(r));
}

test("cache name is versioned", () => {
  assert.match(swConfig().CACHE_NAME, /^ekg-trainer-v\d+$/);
});

// Root-level files that are tooling, not part of the app.
const NOT_APP = new Set(["service-worker.js", "playwright.config.js", "package.json", "package-lock.json"]);
const APP_FILE = /\.(html|js|css|webmanifest|png|svg|ico|json)$/;
const appFiles = () => fs.readdirSync(ROOT).filter((f) => APP_FILE.test(f) && !NOT_APP.has(f) && fs.statSync(path.join(ROOT, f)).isFile());

test("precache list has no duplicates", () => {
  const list = swConfig().CORE_ASSETS;
  assert.equal(new Set(list).size, list.length);
});

test("every app file in the repo root (ekg-*.js/css/html, pages, icons) is precached", () => {
  const cached = new Set(swConfig().CORE_ASSETS);
  const files = appFiles();
  assert.ok(files.some((f) => /^ekg-.*\.js$/.test(f)), "found no ekg-*.js files — is ROOT right?");
  const missing = files.filter((f) => !cached.has(f));
  assert.deepEqual(missing, [], "add these to CORE_ASSETS in service-worker.js, or offline mode breaks");
});

test("every precached file exists on disk", () => {
  for (const asset of swConfig().CORE_ASSETS) {
    if (asset === "./") continue;
    assert.ok(fs.existsSync(path.join(ROOT, asset)), `precached ${asset} is missing`);
  }
});

test("every script/stylesheet/manifest/icon the pages load is precached", () => {
  const cached = new Set(swConfig().CORE_ASSETS);
  for (const page of appFiles().filter((f) => f.endsWith(".html"))) {
    assert.ok(cached.has(page), `${page} not precached`);
    for (const ref of referencedAssets(page)) assert.ok(cached.has(ref), `${page} loads ${ref} but it is not precached`);
  }
  const manifest = JSON.parse(fs.readFileSync(path.join(ROOT, "manifest.webmanifest"), "utf8"));
  for (const icon of manifest.icons) assert.ok(cached.has(icon.src), `manifest icon ${icon.src} not precached`);
  assert.ok(cached.has(manifest.start_url.replace(/^\.\//, "")), "start_url not precached");
});

test("every ekg-*.js module is both loaded by the trainer and precached", () => {
  const cached = new Set(swConfig().CORE_ASSETS);
  const loaded = new Set(referencedAssets("ekg-test.html"));
  const modules = fs.readdirSync(ROOT).filter((f) => /^ekg-.*\.js$/.test(f));
  for (const m of modules) {
    assert.ok(cached.has(m), `${m} is not precached — add it to CORE_ASSETS and bump CACHE_VERSION`);
    assert.ok(loaded.has(m), `${m} exists but ekg-test.html never loads it`);
  }
});
