const CACHE_NAME = "ekg-trainer-v5";
const CORE_ASSETS = [
  "ekg-test.html",
  "ekg.css",
  "styles.css",
  "ekg-rhythms.js",
  "ekg-questions.js",
  "ekg-scenarios.js",
  "ekg-stats.js",
  "ekg-generator.js",
  "ekg-monitor.js",
  "ekg-test.js",
  "ekg-scenario.js",
  "manifest.webmanifest",
  "icon-192.png",
  "icon-512.png",
];

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(CORE_ASSETS)).then(() => self.skipWaiting())
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

// Cache-first for core app assets so the simulation launches offline;
// network-first fallback for anything else (e.g. the study-hub board page).
self.addEventListener("fetch", (event) => {
  if (event.request.method !== "GET") return;

  event.respondWith(
    caches.match(event.request).then((cached) => {
      if (cached) return cached;
      return fetch(event.request)
        .then((response) => {
          if (response.ok && response.type === "basic") {
            const clone = response.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(event.request, clone));
          }
          return response;
        })
        .catch(() => cached);
    })
  );
});
