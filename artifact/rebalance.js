#!/usr/bin/env node
/*
 * Option-length rebalancing helper.
 *   node rebalance.js list <margin> <from> <to>   print items where the keyed option is the longest by >= margin chars
 *   node rebalance.js apply <patchFile>           patch lines: id|k|new distractor text  (k = 1..3, source order)
 * The reason after " | " is kept; only the distractor wording changes.
 */
const fs = require("fs");
const path = require("path");
const { parseAll } = require("./build.js");
const SRC = path.join(__dirname, "questions");

function cands(margin) {
  return parseAll().questions.filter((q) => {
    const L = q.o.map((x) => x.text.length);
    return L[0] - Math.max(L[1], L[2], L[3]) >= margin;
  });
}

const cmd = process.argv[2];
if (cmd === "list") {
  const all = cands(Number(process.argv[3] || 12));
  const from = Number(process.argv[4] || 0), to = Number(process.argv[5] || all.length);
  console.log(`# ${all.length} candidates; showing ${from}-${Math.min(to, all.length)}`);
  all.slice(from, to).forEach((q) => {
    const L = q.o.map((x) => x.text.length);
    let k = 1; for (let i = 2; i <= 3; i++) if (L[i] > L[k]) k = i;
    console.log(`${q.id}|${k}| ${q.s.slice(0, 80)} || C(${L[0]}): ${q.o[0].text} || D: ${q.o[k].text}`);
  });
} else if (cmd === "apply") {
  const byId = {};
  parseAll().questions.forEach((q) => { byId[q.id] = q; });
  const files = {};
  let ok = 0; const bad = [];
  for (const raw of fs.readFileSync(process.argv[3], "utf8").split("\n")) {
    if (!raw.trim() || raw.startsWith("#")) continue;
    const a = raw.indexOf("|"), b = raw.indexOf("|", a + 1);
    const id = raw.slice(0, a).trim(), k = Number(raw.slice(a + 1, b)), text = raw.slice(b + 1).trim();
    const q = byId[id];
    if (!q || !(k >= 1 && k <= 3) || !text || text.includes(" | ")) { bad.push(`${id}: bad patch line`); continue; }
    if (text.length <= q.o[0].text.length) bad.push(`${id}: still shorter than key (${text.length} vs ${q.o[0].text.length})`);
    const lines = files[q.file] || (files[q.file] = fs.readFileSync(path.join(SRC, q.file), "utf8").split("\n"));
    let seen = 0, done = false;
    for (let i = q.line; i < lines.length && lines[i].trim(); i++) {
      if (lines[i][0] === "-") {
        seen++;
        if (seen === k) {
          const cut = lines[i].indexOf(" | ");
          lines[i] = "- " + text + lines[i].slice(cut);
          done = true; break;
        }
      }
    }
    if (done) ok++; else bad.push(`${id}: option ${k} not found`);
  }
  for (const f of Object.keys(files)) fs.writeFileSync(path.join(SRC, f), files[f].join("\n"));
  console.log(`applied ${ok}; problems ${bad.length}`);
  bad.forEach((x) => console.log("  " + x));
} else {
  console.log("usage: node rebalance.js list <margin> [from] [to] | apply <patchFile>");
}
