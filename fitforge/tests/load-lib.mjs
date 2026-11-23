// Compiles App.jsx with esbuild (installed with Vite) so the tests run against
// the real movement library rather than a hand-copied fixture that could drift.
import { build } from "esbuild";
import { mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");

export async function loadApp() {
  const out = await build({
    entryPoints: [join(root, "src/App.jsx")],
    bundle: true,
    format: "esm",
    platform: "node",
    jsx: "automatic",
    write: false,
    logLevel: "silent",
  });
  const dir = mkdtempSync(join(tmpdir(), "fitforge-test-"));
  const file = join(dir, "app.mjs");
  writeFileSync(file, out.outputFiles[0].text);
  return import(pathToFileURL(file).href);
}
