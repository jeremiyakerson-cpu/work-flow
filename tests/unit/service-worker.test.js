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

test("every precached file exists on disk", () => {
  for (const asset of swConfig().CORE_ASSETS) {
    if (asset === "./") continue;
    assert.ok(fs.existsSync(path.join(ROOT, asset)), `precached ${asset} is missing`);
  }
});

test("every script/stylesheet/manifest/icon the pages load is precached", () => {
  const cached = new Set(swConfig().CORE_ASSETS);
  for (const page of ["ekg-test.html", "index.html"]) {
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
