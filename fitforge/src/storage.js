/* The app calls window.storage.get/set, which exists inside the Claude artifact
   sandbox but not in a normal browser. This shim provides the same async API on
   top of localStorage so the exact same App.jsx runs in both places unchanged.

   Note the get() contract: a missing key throws rather than returning null,
   matching the sandbox behavior the app already guards against with try/catch. */

const PREFIX = "fitforge:";

if (typeof window !== "undefined" && !window.storage) {
  window.storage = {
    async get(key) {
      const raw = localStorage.getItem(PREFIX + key);
      if (raw === null) throw new Error(`No value for key: ${key}`);
      return { key, value: raw, shared: false };
    },
    async set(key, value) {
      localStorage.setItem(PREFIX + key, value);
      return { key, value, shared: false };
    },
    async delete(key) {
      localStorage.removeItem(PREFIX + key);
      return { key, deleted: true, shared: false };
    },
    async list(prefix = "") {
      const keys = [];
      for (let i = 0; i < localStorage.length; i++) {
        const k = localStorage.key(i);
        if (k && k.startsWith(PREFIX + prefix)) keys.push(k.slice(PREFIX.length));
      }
      return { keys, prefix, shared: false };
    },
  };
}
