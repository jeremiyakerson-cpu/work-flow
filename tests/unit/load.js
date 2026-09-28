// Loads the app's browser scripts into an isolated vm context with a
// fake window/localStorage/document, so pure logic can be unit-tested
// in Node without a browser or a build step.
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const ROOT = path.resolve(__dirname, "..", "..");

function makeStorage(initial = {}) {
  const map = new Map(Object.entries(initial));
  const storage = {
    getItem: (k) => (map.has(k) ? map.get(k) : null),
    setItem: (k, v) => map.set(k, String(v)),
    removeItem: (k) => map.delete(k),
    clear: () => map.clear(),
    key: (i) => [...map.keys()][i] ?? null,
    get length() {
      return map.size;
    },
    _map: map,
  };
  // Object.keys(localStorage) is used by the app — expose keys as props.
  return new Proxy(storage, {
    ownKeys: () => [...map.keys()],
    getOwnPropertyDescriptor: (t, k) =>
      map.has(k) ? { enumerable: true, configurable: true, value: map.get(k) } : Reflect.getOwnPropertyDescriptor(t, k),
  });
}

// A permissive stand-in for DOM elements: any property read returns
// something harmless so module init() code can run.
function fakeElement() {
  const el = {
    hidden: false,
    textContent: "",
    innerHTML: "",
    className: "",
    style: {},
    dataset: {},
    classList: { add() {}, remove() {}, toggle() {}, contains: () => false },
    addEventListener() {},
    appendChild() {},
    setAttribute() {},
    querySelectorAll: () => [],
    querySelector: () => null,
    getContext: () => null,
  };
  return el;
}

function load(files, opts = {}) {
  const elements = new Map();
  const document = {
    readyState: "complete",
    getElementById(id) {
      if (!elements.has(id)) elements.set(id, fakeElement());
      return elements.get(id);
    },
    querySelectorAll: () => [],
    addEventListener() {},
    createElement: () => fakeElement(),
  };
  const ctx = {
    console,
    localStorage: opts.localStorage || makeStorage(),
    document,
    setTimeout,
    clearTimeout,
    setInterval: () => 0,
    clearInterval() {},
    requestAnimationFrame: () => 0,
    confirm: () => true,
    Math,
    Date,
    JSON,
  };
  ctx.window = ctx;
  ctx.self = ctx;
  vm.createContext(ctx);
  // Top-level `const` in classic scripts is shared across scripts in the
  // same realm, so concatenate like the browser's global scope would.
  const src = files.map((f) => fs.readFileSync(path.join(ROOT, f), "utf8")).join("\n;\n");
  vm.runInContext(src, ctx, { filename: files.join("+") });
  ctx.__elements = elements;
  return ctx;
}

module.exports = { load, makeStorage, ROOT };
