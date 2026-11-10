#!/usr/bin/env node
/*
 * CEN Study Hub question bank tooling.
 *
 *   node build.js check            validate every source file, print counts per topic
 *   node build.js blind <outDir>   write key-stripped, shuffled review files (one per domain)
 *   node build.js compare <dir>    compare reviewer answers in <dir>/review-dNN.json with the key
 *   node build.js build            inject the bank into template.html -> cen-study-hub.html
 *
 * Source format (questions/*.txt), blank line between questions:
 *   @ 1.0                      domain.topicIndex for the questions that follow
 *   ? stem
 *   + correct option
 *   - wrong option | one-line reason it is wrong      (exactly three)
 *   = rationale for the correct option
 *   ! optional note: a fact in this item that still needs RN/source confirmation
 *
 * Options are written correct-first and shuffled deterministically per question id,
 * so the key position is balanced and stable between builds.
 */
const fs = require("fs");
const path = require("path");

const DIR = __dirname;
const SRC = path.join(DIR, "questions");

// topics per domain on the live board (order matters: progress is stored by index)
const TOPIC_COUNTS = { 1: 9, 2: 9, 3: 9, 4: 9, 5: 9, 6: 9, 7: 8, 8: 9, 9: 9, 10: 9, 11: 9 };
// minimum questions per topic, weighted by the July 2026 blueprint item counts
const TARGET = { 1: 12, 5: 12, 6: 12, 7: 12, 10: 11, 2: 11, 9: 11, 4: 10, 3: 10, 8: 10, 11: 10 };

function hash(str) {
  let h = 2166136261;
  for (let i = 0; i < str.length; i++) { h ^= str.charCodeAt(i); h = Math.imul(h, 16777619); }
  return h >>> 0;
}
function rng(seed) {
  let s = seed || 1;
  return () => { s ^= s << 13; s >>>= 0; s ^= s >>> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; };
}

function parseAll() {
  const files = fs.readdirSync(SRC).filter((f) => f.endsWith(".txt")).sort();
  const out = [];
  const errors = [];
  const seq = {};
  for (const f of files) {
    const lines = fs.readFileSync(path.join(SRC, f), "utf8").split("\n");
    let topic = null;
    let cur = null;
    const flush = (ln) => {
      if (!cur) return;
      const where = `${f}:${cur.line}`;
      if (!topic) errors.push(`${where} question before any @ topic line`);
      if (!cur.s) errors.push(`${where} missing stem`);
      if (cur.o.length !== 4) errors.push(`${where} needs 1 correct + 3 wrong options, has ${cur.o.length}`);
      if (cur.correct !== 1) errors.push(`${where} needs exactly one + line`);
      if (!cur.r) errors.push(`${where} missing rationale`);
      const norm = cur.o.map((x) => x.text.toLowerCase().replace(/[^a-z0-9]+/g, " ").trim());
      if (new Set(norm).size !== norm.length) errors.push(`${where} options are not distinct`);
      cur.o.forEach((x, i) => { if (i > 0 && !x.why) errors.push(`${where} wrong option ${i} has no reason`); });
      if (topic) {
        const key = topic;
        seq[key] = (seq[key] || 0) + 1;
        const [d, t] = key.split(".").map(Number);
        out.push({ id: `${key}.${seq[key]}`, d, t, file: f, line: cur.line, s: cur.s, o: cur.o, r: cur.r, f: cur.f || null });
      }
      cur = null;
    };
    lines.forEach((raw, i) => {
      const line = raw.trimEnd();
      if (!line.trim()) { flush(); return; }
      const tag = line[0];
      const body = line.slice(1).trim();
      if (tag === "#") return;
      if (tag === "@") {
        flush();
        const m = body.match(/^(\d+)\.(\d+)$/);
        if (!m || !TOPIC_COUNTS[m[1]] || Number(m[2]) >= TOPIC_COUNTS[m[1]]) errors.push(`${f}:${i + 1} bad topic "${body}"`);
        topic = body;
        return;
      }
      if (tag === "?") { flush(); cur = { line: i + 1, s: body, o: [], correct: 0, r: "", f: "" }; return; }
      if (!cur) { errors.push(`${f}:${i + 1} stray line`); return; }
      if (tag === "+") { cur.correct++; cur.o.unshift({ text: body, why: null }); return; }
      if (tag === "-") {
        const k = body.indexOf(" | ");
        if (k < 0) { cur.o.push({ text: body, why: "" }); return; }
        cur.o.push({ text: body.slice(0, k).trim(), why: body.slice(k + 3).trim() });
        return;
      }
      if (tag === "=") { cur.r = body; return; }
      if (tag === "!") { cur.f = body; return; }
      errors.push(`${f}:${i + 1} unknown tag "${tag}"`);
    });
    flush();
  }
  return { questions: out, errors };
}

function shuffled(q) {
  const order = [0, 1, 2, 3];
  const r = rng(hash(q.id + "|" + q.s.slice(0, 40)));
  for (let i = 3; i > 0; i--) { const j = Math.floor(r() * (i + 1)); [order[i], order[j]] = [order[j], order[i]]; }
  return {
    id: q.id, d: q.d, t: q.t, s: q.s,
    o: order.map((k) => q.o[k].text),
    a: order.indexOf(0),
    r: q.r,
    w: order.map((k) => q.o[k].why),
    f: q.f || undefined,
  };
}

function counts(questions) {
  const c = {};
  for (const q of questions) { const k = `${q.d}.${q.t}`; c[k] = (c[k] || 0) + 1; }
  return c;
}

function check() {
  const { questions, errors } = parseAll();
  const c = counts(questions);
  let short = 0;
  const warn = [];
  for (const q of questions) {
    const lens = q.o.map((x) => x.text.length);
    const wrongMean = (lens[1] + lens[2] + lens[3]) / 3;
    if (lens[0] > wrongMean * 1.7 && lens[0] - wrongMean > 25) warn.push(`${q.file}:${q.line} correct option much longer than distractors (${lens[0]} vs ~${Math.round(wrongMean)})`);
    if (!/[?:.]$/.test(q.s)) warn.push(`${q.file}:${q.line} stem does not end in ? : or .`);
  }
  const stems = {};
  for (const q of questions) { const k = q.s.toLowerCase().slice(0, 70); if (stems[k]) warn.push(`${q.file}:${q.line} stem starts like ${stems[k]}`); else stems[k] = q.id; }
  for (const d of Object.keys(TOPIC_COUNTS)) {
    const row = [];
    for (let t = 0; t < TOPIC_COUNTS[d]; t++) { const n = c[`${d}.${t}`] || 0; if (n < TARGET[d]) short++; row.push(n); }
    console.log(`domain ${String(d).padStart(2)}  target ${TARGET[d]}/topic  ${row.join(" ")}  = ${row.reduce((a, b) => a + b, 0)}`);
  }
  const dist = [0, 0, 0, 0];
  questions.map(shuffled).forEach((q) => dist[q.a]++);
  console.log(`total ${questions.length} | key positions A-D: ${dist.join(" ")} | flagged for RN review: ${questions.filter((q) => q.f).length}`);
  console.log(`topics under target: ${short} | warnings: ${warn.length} | errors: ${errors.length}`);
  if (process.argv.includes("-v")) warn.forEach((w) => console.log("warn  " + w));
  errors.forEach((e) => console.log("ERROR " + e));
  return { questions, errors, short };
}

function blind(outDir, only) {
  const { questions, errors } = parseAll();
  if (errors.length) { errors.forEach((e) => console.log("ERROR " + e)); process.exit(1); }
  fs.mkdirSync(outDir, { recursive: true });
  const ids = only ? new Set(fs.readFileSync(only, "utf8").split(/\s+/).filter(Boolean)) : null;
  const byD = {};
  for (const q of questions.map(shuffled)) {
    if (ids && !ids.has(q.id)) continue;
    (byD[q.d] = byD[q.d] || []).push({ id: q.id, stem: q.s, A: q.o[0], B: q.o[1], C: q.o[2], D: q.o[3] });
  }
  for (const d of Object.keys(byD)) {
    const name = ids ? "recheck" : `blind-d${String(d).padStart(2, "0")}`;
    if (ids) continue;
    fs.writeFileSync(path.join(outDir, name + ".json"), JSON.stringify(byD[d], null, 1));
    console.log(`${name}.json ${byD[d].length}`);
  }
  if (ids) {
    const all = [].concat(...Object.values(byD));
    fs.writeFileSync(path.join(outDir, "blind-recheck.json"), JSON.stringify(all, null, 1));
    console.log(`blind-recheck.json ${all.length}`);
  }
}

function compare(dir) {
  const { questions } = parseAll();
  const key = {};
  questions.map(shuffled).forEach((q) => { key[q.id] = q; });
  const L = "ABCD";
  let seen = 0, agree = 0;
  const fails = [];
  for (const f of fs.readdirSync(dir).filter((x) => /^review-.*\.json$/.test(x)).sort()) {
    let rows;
    try { rows = JSON.parse(fs.readFileSync(path.join(dir, f), "utf8")); } catch (e) { console.log(`unreadable ${f}: ${e.message}`); continue; }
    for (const r of rows) {
      const q = key[r.id];
      if (!q) { console.log(`unknown id ${r.id} in ${f}`); continue; }
      seen++;
      const keyed = L[q.a];
      const ok = String(r.answer).trim().toUpperCase() === keyed;
      const alt = r.also_defensible && String(r.also_defensible).trim() ? String(r.also_defensible).trim() : "";
      const concern = r.concern && String(r.concern).trim() ? String(r.concern).trim() : "";
      if (ok && !alt && !concern) { agree++; continue; }
      fails.push({ id: r.id, file: f, keyed, reviewer: r.answer, alt, concern, kind: !ok ? "DISAGREE" : alt ? "TWO-DEFENSIBLE" : "CONCERN" });
    }
  }
  console.log(`reviewed ${seen} | clean agreement ${agree} | flagged ${fails.length}`);
  const by = {};
  fails.forEach((x) => { by[x.kind] = (by[x.kind] || 0) + 1; });
  console.log(JSON.stringify(by));
  fs.writeFileSync(path.join(dir, "flagged.json"), JSON.stringify(fails, null, 1));
  return fails;
}

function build() {
  const { questions, errors, short } = check();
  if (errors.length) process.exit(1);
  const bank = questions.map(shuffled);
  const tpl = fs.readFileSync(path.join(DIR, "template.html"), "utf8");
  if (!tpl.includes("/*__BANK__*/")) throw new Error("template.html has no /*__BANK__*/ placeholder");
  const json = JSON.stringify(bank).replace(/</g, "\\u003c");
  fs.writeFileSync(path.join(DIR, "cen-study-hub.html"), tpl.replace("/*__BANK__*/", () => json));
  console.log(`built cen-study-hub.html with ${bank.length} questions (${short} topics under target)`);
}

const cmd = require.main === module ? process.argv[2] : null;
if (cmd === null) { /* loaded as a module */ }
else if (cmd === "check") { const r = check(); process.exit(r.errors.length ? 1 : 0); }
else if (cmd === "blind") blind(process.argv[3], process.argv[4]);
else if (cmd === "compare") compare(process.argv[3]);
else if (cmd === "build") build();
else { console.log("usage: node build.js check|blind <dir>|compare <dir>|build"); process.exit(1); }

module.exports = { parseAll, shuffled, TARGET, TOPIC_COUNTS };
