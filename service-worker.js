// Offline support for the EKG trainer (and the study-hub board page).
//
// Bump CACHE_VERSION whenever CORE_ASSETS changes; old caches are deleted
// on activate. tests/unit/service-worker.test.js fails if a page references
// a file (or an ekg-*.js exists) that is not precached here.
const CACHE_VERSION = 9;
const CACHE_PREFIX = "ekg-trainer-";
const CACHE_NAME = `${CACHE_PREFIX}v${CACHE_VERSION}`;
const CORE_ASSETS = [
  "./",
  "index.html",
  "app.js",
  "ekg-test.html",
  "ekg.css",
  "styles.css",
  "ekg-rhythms.js",
  "ekg-questions.js",
  "ekg-scenarios.js",
  "ekg-education.js",
  "ekg-stats.js",
  "ekg-generator.js",
  "ekg-monitor.js",
  "ekg-codelog.js",
  "ekg-test.js",
  "ekg-scenario.js",
  "ekg-study.js",
  "ekg-sprint.js",
  "ekg-progress.js",
  "manifest.webmanifest",
  "icon.svg",
  "icon-192.png",
  "icon-512.png",
];

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches
      .open(CACHE_NAME)
      // "reload" bypasses the HTTP cache so a new version never precaches stale files
      .then((cache) => cache.addAll(CORE_ASSETS.map((url) => new Request(url, { cache: "reload" }))))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) =>
        Promise.all(keys.filter((k) => k.startsWith(CACHE_PREFIX) && k !== CACHE_NAME).map((k) => caches.delete(k)))
      )
      .then(() => self.clients.claim())
  );
});

// Stale-while-revalidate for same-origin GETs: answer instantly from the
// cache (so the app launches offline), refresh the cached copy in the
// background so content updates land on the next visit. Navigations that
// miss the cache while offline fall back to the trainer page.
self.addEventListener("fetch", (event) => {
  const { request } = event;
  if (request.method !== "GET") return;
  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;

  event.respondWith(
    caches.open(CACHE_NAME).then(async (cache) => {
      const cached = await cache.match(request, { ignoreSearch: true });
      const network = fetch(request)
        .then((response) => {
          if (response.ok && response.type === "basic") cache.put(request, response.clone());
          return response;
        })
        .catch(() => null);

      if (cached) {
        event.waitUntil(network);
        return cached;
      }
      const response = await network;
      if (response) return response;
      if (request.mode === "navigate") {
        const fallback = await cache.match("ekg-test.html");
        if (fallback) return fallback;
      }
      return new Response("Offline and not cached", { status: 503, statusText: "Offline" });
    })
  );
});
