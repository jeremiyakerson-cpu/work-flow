// Packs the production build into one self-contained page for publishing as a
// claude.ai artifact: CSS and JS inlined, no service worker or manifest (an
// artifact frame allows neither). Run after `vite build`:
//   npm run build:artifact   ->   dist/fitforge-artifact.html
import { readFileSync, writeFileSync, readdirSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const dist = join(dirname(fileURLToPath(import.meta.url)), "..", "dist");
const assets = readdirSync(join(dist, "assets"));
const css = assets.filter((f) => f.endsWith(".css")).map((f) => readFileSync(join(dist, "assets", f), "utf8")).join("\n");
const jsFiles = assets.filter((f) => f.endsWith(".js"));
if (jsFiles.length !== 1) throw new Error(`expected one JS chunk, found ${jsFiles.length}`);
// An inline script ends at the first "</script", wherever it appears.
const js = readFileSync(join(dist, "assets", jsFiles[0]), "utf8").replace(/<\/script/gi, "<\\/script");

const page = `<title>FitForge</title>
<style>
/* FitForge commits to one light look ("Daylight"); it doesn't follow the
   viewer's dark mode, so the ground and color scheme are set explicitly. */
:root { color-scheme: light; }
html, body, #root { min-height: 100%; }
body { margin: 0; background: #F6F5F2; color: #16181D; }
${css}
</style>
<div id="root"></div>
<script type="module">
${js}
</script>
`;
const out = join(dist, "fitforge-artifact.html");
writeFileSync(out, page);
console.log(`wrote ${out} (${(page.length / 1024).toFixed(0)} KB)`);
