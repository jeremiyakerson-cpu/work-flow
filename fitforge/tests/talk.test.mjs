// Unit tests for the talk-to-build parser and builder (src/talk.js).
// Run: npm test
import { test } from "node:test";
import assert from "node:assert/strict";
import { parseRequest, mergeIntent, buildTalkWorkout } from "../src/talk.js";
import { loadApp } from "./load-lib.mjs";

const { TALK_LIB: lib } = await loadApp();
const EX = Object.fromEntries(lib.EX.map((e) => [e.id, e]));
const nameToEx = Object.fromEntries(lib.EX.map((e) => [e.name, e]));
const GYM = lib.EQUIPMENT.map((e) => e.id).filter((id) => id !== "pool");

function seeded(seed) {
  let s = seed >>> 0;
  return () => { s = (s * 1664525 + 1013904223) >>> 0; return s / 2 ** 32; };
}
const parse = (t) => parseRequest(t, lib);
const build = (t, ctx = {}, seed = 1) => buildTalkWorkout(parse(t), lib, { availEquip: GYM, rng: seeded(seed), ...ctx });
const moves = (r) => r.wod.movements.map((m) => nameToEx[m.name]);
const ids = (list) => list.map((e) => e.id);

/* ---------------------------------------------------------------- parsing */

test("reads minutes in digits, words and phrases", () => {
  assert.equal(parse("20 minutes legs").minutes, 20);
  assert.equal(parse("twenty five min upper body").minutes, 25);
  assert.equal(parse("half an hour of cardio").minutes, 30);
  assert.equal(parse("give me a 45-min session").minutes, 45);
  assert.equal(parse("an hour at the gym").minutes, 60);
  assert.equal(parse("quick core workout").minutes, 10);
});

test("clamps silly durations and remembers what was asked", () => {
  const i = parse("3 hours of legs");
  assert.equal(i.minutes, 60);
  assert.equal(i.minutesClamped, 180);
  assert.equal(parse("2 min abs").minutes, 5);
});

test("reads intensity, including negated hard", () => {
  assert.equal(parse("something hard for legs").level, "hard");
  assert.equal(parse("not too hard please, 20 minutes").level, "easy");
  assert.equal(parse("destroy me").level, "brutal");
  assert.equal(parse("gentle full body").level, "easy");
  assert.equal(parse("legs").level, null);
});

test("'easy on my knee' is an injury, not an intensity", () => {
  const i = parse("go easy on my knee, 20 min legs");
  assert.deepEqual(i.avoid, ["knee"]);
  assert.equal(i.level, null);
});

test("reads focus areas and keeps 'back' out of non-focus phrases", () => {
  assert.deepEqual(parse("legs and core").focus, ["lower", "core"]);
  assert.deepEqual(parse("chest and triceps").focus, ["chest", "arms"]);
  assert.deepEqual(parse("getting back into it, full body").focus, ["full"]);
  assert.deepEqual(parse("back squats and pull-ups").focus, []);
});

test("an injured body part is avoided, not focused", () => {
  const i = parse("my lower back is sore, upper body today");
  assert.ok(i.avoid.includes("back"));
  assert.deepEqual(i.focus, ["upper"]);
});

test("'no legs today' avoids legs", () => {
  const i = parse("no legs today, 20 min");
  assert.ok(i.avoid.includes("legs") || i.avoidFocus.includes("lower"));
  assert.deepEqual(i.focus, []);
});

test("finds named movements and negated ones, including lists", () => {
  const i = parse("include pull-ups and kettlebell swings, but no burpees, lunges or box jumps");
  const inc = i.include.flatMap((e) => e.ids);
  const exc = i.exclude.flatMap((e) => e.ids || []);
  assert.ok(inc.includes("pullup"), "pull-ups included");
  assert.ok(inc.includes("kbswing"), "swings included");
  assert.ok(exc.includes("burpee") && exc.includes("lunge") && exc.includes("boxjump"), `excluded: ${exc}`);
  assert.ok(!inc.includes("burpee"));
});

test("'swap X for Y' excludes X and includes Y", () => {
  const i = parse("swap the burpees for push ups");
  assert.ok(i.exclude.flatMap((e) => e.ids).includes("burpee"));
  assert.ok(i.include.flatMap((e) => e.ids).includes("pushup"));
});

test("'dead bugs for core' is a movement, not a dead core", () => {
  const i = parse("dead bugs for core");
  assert.deepEqual(i.avoid, []);
  assert.ok(i.include.flatMap((e) => e.ids).includes("deadbug"));
  assert.deepEqual(i.focus, ["core"]);
});

test("reads equipment, bodyweight-only and negated kit", () => {
  assert.deepEqual(parse("quick upper body with dumbbells").equipment, ["dumbbells"]);
  assert.equal(parse("hotel room workout, 15 min").bodyweightOnly, true);
  assert.equal(parse("no equipment, legs").bodyweightOnly, true);
  const i = parse("I don't have a bench but I have dumbbells");
  assert.deepEqual(i.noEquipment, ["bench"]);
  assert.deepEqual(i.equipment, ["dumbbells"]);
});

test("'home gym' means saved kit, 'at the gym' means everything", () => {
  assert.equal(parse("home gym session").fullGym, false);
  assert.equal(parse("at the gym today").fullGym, true);
});

test("reads format and goals", () => {
  assert.equal(parse("20 minute emom").format, "EMOM");
  assert.equal(parse("tabata please").format, "Tabata");
  assert.equal(parse("I want to get stronger").goal, "strength");
  assert.equal(parse("build muscle in my arms").goal, "muscle");
});

test("box breathing is breathwork, not a plyo box", () => {
  const i = parse("box breathing to calm down");
  assert.deepEqual(i.equipment, []);
  assert.equal(i.recovery.breath, "physio");
});

test("red flags are detected", () => {
  assert.equal(parse("chest pain but want a hard workout").redFlag, true);
  assert.equal(parse("I felt dizzy yesterday, legs?").redFlag, true);
  assert.equal(parse("chest and back day").redFlag, false);
});

/* ---------------------------------------------------------------- building */

test("every built movement exists in the library", () => {
  const asks = ["legs", "upper body dumbbells", "20 min emom", "tabata core", "chipper 30 minutes", "strength", "build muscle", "cardio", "hotel room 10 min", "full body hard 45 min"];
  for (const a of asks) {
    for (let seed = 1; seed <= 20; seed++) {
      const r = build(a, {}, seed);
      assert.equal(r.kind, "workout", a);
      for (const m of r.wod.movements) assert.ok(nameToEx[m.name], `${a}: unknown movement ${m.name}`);
      assert.ok(r.wod.movements.length >= 1);
    }
  }
});

test("bodyweight-only requests never use equipment", () => {
  for (let seed = 1; seed <= 60; seed++) {
    const r = build("hotel room, 20 minutes, full body, hard", {}, seed);
    for (const e of moves(r)) assert.ok(e.equip.includes("bodyweight"), `${e.name} needs ${e.equip}`);
  }
});

test("named equipment restricts the pool to that kit plus bodyweight", () => {
  for (let seed = 1; seed <= 60; seed++) {
    const r = build("upper body with dumbbells", {}, seed);
    for (const e of moves(r)) assert.ok(e.equip.some((q) => q === "bodyweight" || q === "dumbbells"), `${e.name}`);
  }
});

test("saved kit is used when nothing is named", () => {
  for (let seed = 1; seed <= 40; seed++) {
    const r = build("20 min full body", { availEquip: ["bodyweight", "kettlebell"] }, seed);
    for (const e of moves(r)) assert.ok(e.equip.some((q) => q === "bodyweight" || q === "kettlebell"), `${e.name}`);
  }
});

test("focus is respected", () => {
  const lower = ["Quads", "Hamstrings", "Glutes"];
  for (let seed = 1; seed <= 60; seed++) {
    const r = build("30 min legs", {}, seed);
    for (const e of moves(r)) assert.ok(e.muscle.some((m) => lower.includes(m)), `${e.name} isn't lower body`);
  }
});

test("two focus areas both get covered", () => {
  for (let seed = 1; seed <= 40; seed++) {
    const r = build("20 min legs and core amrap", {}, seed);
    const ms = moves(r);
    assert.ok(ms.some((e) => e.muscle.includes("Core")), "has core");
    assert.ok(ms.some((e) => e.muscle.some((m) => ["Quads", "Hamstrings", "Glutes"].includes(m))), "has legs");
  }
});

test("no-jumping and knee requests leak nothing", () => {
  for (let seed = 1; seed <= 100; seed++) {
    const r = build("20 minutes legs, no jumping, my knee is sore", {}, seed);
    for (const e of moves(r)) {
      assert.notEqual(e.pattern, "jump", `${e.name} jumps`);
      assert.notEqual(e.pattern, "lunge", `${e.name} lunges`);
      assert.ok(!["run400", "shuttlerun", "pistolsquat"].includes(e.id), e.name);
    }
  }
});

test("shoulder injury removes pressing and pulling", () => {
  for (let seed = 1; seed <= 80; seed++) {
    const r = build("full body, my shoulder hurts", {}, seed);
    for (const e of moves(r)) assert.ok(!["press", "pullup", "pushup"].includes(e.pattern), `${e.name}`);
  }
});

test("explicitly excluded movements never appear; included ones always do", () => {
  for (let seed = 1; seed <= 80; seed++) {
    const r = build("20 min full body, include pull ups, no burpees or thrusters", {}, seed);
    const got = ids(moves(r));
    assert.ok(got.includes("pullup"), "pull-ups kept");
    assert.ok(!got.includes("burpee") && !got.includes("thruster"), `${got}`);
  }
});

test("generic 'squats' resolves to a variant the person can do", () => {
  const bw = build("squats and push ups, no equipment");
  assert.ok(ids(moves(bw)).includes("airsquat"));
  const gym = build("squats and bench at the gym");
  assert.ok(ids(moves(gym)).includes("backsquat"));
});

test("easy sessions drop technical movements", () => {
  for (let seed = 1; seed <= 60; seed++) {
    const r = build("easy 20 minute full body at the gym", {}, seed);
    for (const e of moves(r)) assert.ok(!lib.TECHNICAL.has(e.id), `${e.name} is technical`);
  }
});

test("durations land near the target", () => {
  for (const fmt of ["amrap", "emom", "for time", "chipper"]) {
    for (const mins of [10, 20, 30, 45]) {
      for (let seed = 1; seed <= 15; seed++) {
        const r = build(`${mins} minute ${fmt} full body`, {}, seed);
        const est = r.wod.est;
        assert.ok(Math.abs(est - mins) <= Math.max(4, mins * 0.3), `${fmt} ${mins}: estimated ${est}`);
      }
    }
  }
});

test("EMOM work fits inside the minute", () => {
  for (let seed = 1; seed <= 60; seed++) {
    const r = build("20 min emom", {}, seed);
    for (const m of r.wod.movements) {
      const e = nameToEx[m.name];
      const n = parseFloat(m.reps);
      if (e.unit === "reps") assert.ok(n * 2.5 <= 50, `${m.name} ${m.reps}`);
      if (/s$/.test(m.reps) && !/cal/.test(m.reps)) assert.ok(n <= 45, `${m.name} ${m.reps}`);
    }
  }
});

test("units are rendered correctly (no metres on a plank)", () => {
  for (let seed = 1; seed <= 80; seed++) {
    const r = build("30 min core amrap", {}, seed);
    for (const m of r.wod.movements) {
      const e = nameToEx[m.name];
      if (e.unit === "sec") assert.match(m.reps, /^\d+s$/, `${m.name}: ${m.reps}`);
      if (e.unit === "reps") assert.match(m.reps, /^\d+$/, `${m.name}: ${m.reps}`);
    }
  }
});

test("strength requests produce sets × reps on loadable lifts", () => {
  const r = build("strength day, 45 minutes, gym");
  assert.equal(r.wod.format, "Sets x Reps");
  assert.ok(r.wod.movements.length >= 2 && r.wod.movements.length <= 4);
  for (const m of r.wod.movements) assert.match(m.reps, /^\d+×\d+$/);
});

test("recovery-only asks return a recovery suggestion, not a workout", () => {
  const r = build("something to help me sleep");
  assert.equal(r.kind, "recovery");
  assert.equal(r.recovery.breath.id, "478");
  const y = build("stretch my tight hips");
  assert.equal(y.kind, "recovery");
  assert.equal(y.recovery.yoga, "Hips");
  assert.ok(lib.YOGA_AREAS[y.recovery.yoga]);
});

test("red flags stop the builder", () => {
  assert.equal(build("I have chest pain, give me a hard workout").kind, "stop");
});

test("unparseable text still gets a sensible default", () => {
  const r = build("hello there");
  assert.equal(r.kind, "workout");
  assert.equal(r.wod.target, 20);
});

/* ---------------------------------------------------------------- follow-ups */

test("follow-ups adjust the previous request", () => {
  let i = parse("20 min legs, dumbbells");
  i = mergeIntent(i, parse("harder"));
  assert.equal(i.level, "hard");
  assert.deepEqual(i.focus, ["lower"]);
  assert.deepEqual(i.equipment, ["dumbbells"]);

  i = mergeIntent(i, parse("make it shorter"));
  assert.equal(i.minutes, 10);

  i = mergeIntent(i, parse("no lunges"));
  assert.ok(i.exclude.flatMap((e) => e.ids || []).includes("lunge"));

  i = mergeIntent(i, parse("actually add lunges back"));
  assert.ok(!i.exclude.flatMap((e) => e.ids || []).includes("lunge"));
  assert.ok(i.include.flatMap((e) => e.ids).includes("lunge"));

  i = mergeIntent(i, parse("start over, 15 minutes core"));
  assert.deepEqual(i.focus, ["core"]);
  assert.deepEqual(i.equipment, []);
  assert.equal(i.level, null);
});

test("harder/easier clamp at the ends", () => {
  let i = parse("brutal legs");
  i = mergeIntent(i, parse("harder"));
  assert.equal(i.level, "brutal");
  let j = parse("easy legs");
  j = mergeIntent(j, parse("easier"));
  assert.equal(j.level, "easy");
});

/* ---------------------------------------------------------------- regressions found by reading output */

test("a bench or rack alone doesn't unlock barbell lifts", () => {
  const kit = ["bodyweight", "dumbbells", "bench"];
  for (let seed = 1; seed <= 80; seed++) {
    const r = build("30 min upper body", { availEquip: kit }, seed);
    for (const e of moves(r)) {
      const ok = e.equip.includes("bodyweight") || e.equip.includes("dumbbells") || e.id === "tricepdip" || e.id === "stepup";
      assert.ok(ok, `${e.name} needs ${e.equip}`);
    }
  }
});

test("tabata padding uses conditioning or simple bodyweight moves only", () => {
  for (let seed = 1; seed <= 60; seed++) {
    const r = build("tabata 12 minutes, burpees and mountain climbers", { availEquip: GYM }, seed);
    for (const e of moves(r)) {
      assert.ok(!e.prop || e.cardio || ["swing", "jump"].includes(e.pattern) || ["thruster", "wallball", "devilpress", "manmaker", "medicineballslam"].includes(e.id), `${e.name} in a tabata`);
      assert.notEqual(e.pattern, "plank", `${e.name} is a hold`);
    }
  }
});

test("muscle and strength days skip technical gymnastics and conditioning moves", () => {
  for (let seed = 1; seed <= 60; seed++) {
    for (const ask of ["build muscle in my arms and shoulders, 40 minutes", "strength day at the gym"]) {
      const r = build(ask, {}, seed);
      for (const e of moves(r)) {
        assert.ok(!lib.TECHNICAL.has(e.id), `${e.name} is technical`);
        assert.ok(!e.cardio, `${e.name} is conditioning`);
      }
    }
  }
});

test("bodyweight moves don't get told to pick a weight", () => {
  for (let seed = 1; seed <= 40; seed++) {
    const r = build("build muscle, upper body, 40 min", {}, seed);
    for (const m of r.wod.movements) {
      const e = nameToEx[m.name];
      if (e.equip.includes("bodyweight") && !e.prop) assert.equal(m.load, undefined, `${m.name}: ${m.load}`);
    }
  }
});

test("backstory isn't a request: 'stretch after a run' is a stretch", () => {
  const r = build("stretch my tight hips after a run");
  assert.equal(r.kind, "recovery");
  assert.equal(r.recovery.yoga, "Hips");
  assert.equal(build("easy legs after yesterday's long run").kind, "workout");
});

test("crawls and carries use short distances", () => {
  for (let seed = 1; seed <= 80; seed++) {
    const r = build("25 min hotel room, sweaty, quiet", {}, seed);
    for (const m of r.wod.movements) {
      const e = nameToEx[m.name];
      if (e.pattern === "carry" && e.unit === "m") assert.ok(parseFloat(m.reps) <= 60, `${m.name} ${m.reps}`);
    }
    assert.ok(r.wod.movements.length >= 3, "hotel conditioning has enough moves");
  }
});

test("spoken reply reads naturally", () => {
  assert.match(build("easy 15 min core").say, /^Here's an easy 15-minute/);
  assert.match(build("hard 20 min legs").say, /^Here's a hard 20-minute/);
});

test("refinements keep the workout and only change what was asked", () => {
  const first = buildTalkWorkout(parse("20 min legs and core with kettlebell"), lib, { availEquip: GYM, rng: seeded(4) });
  let intent = mergeIntent(parse("20 min legs and core with kettlebell"), parse("harder"));
  const harder = buildTalkWorkout(intent, lib, { availEquip: GYM, rng: seeded(99), keep: first.keep });
  assert.equal(harder.keep.format, first.keep.format, "same format");
  assert.deepEqual(harder.keep.ids.slice(0, first.keep.ids.length).sort(), first.keep.ids.slice(0, harder.keep.ids.length).sort(), "same movements");
  assert.equal(harder.wod.level, "hard");

  const victim = first.keep.ids[0];
  const name = lib.EX.find((e) => e.id === victim).name;
  intent = mergeIntent(intent, parse(`swap the ${name} for push ups`));
  const swapped = buildTalkWorkout(intent, lib, { availEquip: GYM, rng: seeded(7), keep: harder.keep });
  assert.ok(!swapped.keep.ids.includes(victim), "swapped out");
  assert.ok(swapped.keep.ids.includes("pushup"), "swapped in");
  for (const id of first.keep.ids.slice(1)) assert.ok(swapped.keep.ids.includes(id), `${id} kept`);
});
