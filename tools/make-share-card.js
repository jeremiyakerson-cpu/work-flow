// Renders share-card.png (1200x627), the image LinkedIn and other sites show
// when someone shares a link to the trainer. The strip is drawn with the
// app's own waveform code, so it always matches what learners see.
// Usage: node tools/make-share-card.js   (needs the Playwright dev dependency)
const { chromium } = require("@playwright/test");
const fs = require("fs");
const path = require("path");

const ROOT = path.resolve(__dirname, "..");
const OUT = path.join(ROOT, "share-card.png");
const RHYTHM = "afib";
const HR = 132;
const RHYTHM_COUNT = (() => {
  const src = fs.readFileSync(path.join(ROOT, "ekg-education.js"), "utf8");
  const body = src.slice(src.indexOf("const RHYTHM_GUIDE"), src.indexOf("const RHYTHM_GROUPS"));
  return (body.match(/^  [a-z0-9_]+: \{$/gm) || []).length;
})();

const html = `<!doctype html><meta charset="utf-8"><style>
  html,body{margin:0;width:1200px;height:627px;background:#0b0f14;overflow:hidden;
    font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Inter,Roboto,Helvetica,Arial,sans-serif;color:#e7edf5}
  .wrap{position:absolute;inset:0;padding:56px 64px;display:flex;flex-direction:column}
  .kicker{font-size:22px;font-weight:700;letter-spacing:.14em;color:#4fd1c5}
  h1{margin:14px 0 10px;font-size:68px;line-height:1.02;letter-spacing:-.01em}
  .sub{font-size:28px;color:#aab6c4;margin:0}
  .monitor{margin-top:auto;display:flex;gap:18px;background:#05080b;border:2px solid #1d252f;border-radius:18px;padding:16px}
  canvas{flex:1;min-width:0;width:100%;height:200px;border-radius:8px;display:block}
  .tile{flex:none;box-sizing:border-box;width:190px;border:1px solid #1d252f;border-radius:10px;padding:14px 16px;display:flex;flex-direction:column}
  .tl{font-size:18px;letter-spacing:.08em;color:#8b97a7}
  .tv{font-size:76px;font-weight:700;color:#39ff8f;line-height:1;margin-top:10px}
  .foot{position:absolute;right:64px;top:62px;font-size:18px;color:#7f8b99;letter-spacing:.04em}
</style>
<div class="wrap">
  <div class="kicker">EKG RHYTHM TRAINER</div>
  <h1>Read rhythms on a live monitor</h1>
  <p class="sub">${RHYTHM_COUNT} rhythms · code scenarios · timed exams · study guide</p>
  <div class="monitor"><canvas id="c" width="1880" height="400"></canvas>
    <div class="tile"><span class="tl">HR bpm</span><span class="tv">${HR}</span></div></div>
</div>
<div class="foot">Educational use only</div>
<script>window.matchMedia = () => ({ matches: false, addEventListener() {} });</script>
<script>${fs.readFileSync(path.join(ROOT, "ekg-rhythms.js"), "utf8")}</script>
<script>
  const strip = EkgRhythms.synthesizeRhythm(${JSON.stringify(RHYTHM)}, ${HR}, {});
  const c = document.getElementById("c"), g = c.getContext("2d"), W = c.width, H = c.height;
  g.fillStyle = "#020a06"; g.fillRect(0, 0, W, H);
  g.strokeStyle = "rgba(0,200,110,0.14)"; g.lineWidth = 1;
  for (let x = 0; x <= W; x += 40) { g.beginPath(); g.moveTo(x + .5, 0); g.lineTo(x + .5, H); g.stroke(); }
  for (let y = 0; y <= H; y += 40) { g.beginPath(); g.moveTo(0, y + .5); g.lineTo(W, y + .5); g.stroke(); }
  g.strokeStyle = "#39ff8f"; g.lineWidth = 4; g.lineJoin = "round"; g.shadowColor = "rgba(57,255,143,.6)"; g.shadowBlur = 10;
  g.beginPath();
  for (let i = 0; i < strip.length; i++) { const x = i / strip.length * W, y = H * .58 - strip[i] * H * .42; i ? g.lineTo(x, y) : g.moveTo(x, y); }
  g.stroke();
</script>`;

(async () => {
  const browser = await chromium.launch(process.env.CHROMIUM ? { executablePath: process.env.CHROMIUM } : {});
  const page = await browser.newPage({ viewport: { width: 1200, height: 627 }, deviceScaleFactor: 1 });
  await page.setContent(html);
  await page.screenshot({ path: OUT });
  await browser.close();
  console.log(`wrote ${path.relative(ROOT, OUT)} (${RHYTHM_COUNT} rhythms, ${Math.round(fs.statSync(OUT).size / 1024)} KB)`);
})();
