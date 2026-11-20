# HANDOFF — FitForge & Everyday

Two separate training apps, both single-file React, both offline-capable, both
with PWA scaffolds ready to deploy. This document covers what exists, how it's
built, what's been verified, and what's genuinely still open.

Last updated: end of the build session.

---

## 1. What you have

| | **FitForge** | **Everyday** |
|---|---|---|
| Audience | Trains already, owns equipment | Anyone, anywhere, no kit |
| Movements | 135 | 63 |
| Yoga | 50 poses / 12 areas | 35 poses |
| Stretches | 7 cool-down | 27 dedicated |
| Breathwork | 28 patterns | — |
| Session length | 5 / 10 / 15 / 20 / 30 min | 5 / 10 / 15 min |
| AI coach | Yes | No |
| Equipment | 29 types, filterable | None, ever |
| Theme | Light ("Daylight") | Light, three-colour |
| Source | `FitForge.jsx` (~6,200 lines) | `BodyweightApp.jsx` (~1,400 lines) |
| Project | `fitforge-pwa/` | `everyday-pwa/` |

They share no code. That was deliberate — Everyday exists precisely because
FitForge assumes equipment and prior knowledge, and coupling them would have
dragged those assumptions back in.

---

## 2. FitForge

### Structure
One file, top to bottom: theme tokens → data (equipment, movements, benchmarks,
yoga, breathwork) → helpers (scaling, units, export, charts) → store → screens →
`App`. No build step required to read it; Vite only wraps it for deployment.

### Screens
`home` `generator` `muscle` `cardio` `yoga` `breath` `coach` `progress` `plan`
`favorites` `tools` `search` — twelve, reached through a radial hub.

### The AI coach
Calls `claude-sonnet-4-6` via the Anthropic API from the artifact. Four modes:

- **review** — reads your log, returns strengths/risks/focus as JSON
- **session** — designs a workout, including tempo and %1RM where it has a PR
- **chat** — free conversation, carries prior turns, returns prose not JSON
- **debrief** — reacts to what you wrote after a session

**Context is computed locally** in `buildTrainingContext()` and handed over as
accurate data. The model interprets; it never does arithmetic on your logs. It
sees your equipment and a movement whitelist, and the prompt hard-constrains it
to those. It also remembers its own past advice, including whether you did it.

**Two safety instructions are load-bearing** and shouldn't be removed: never
program through pain, and treat recovery as a legitimate session.

### Storage keys
`ff-settings` `equipment-selection` `ff-history` `ff-prs` `ff-favorites`
`ff-saved-wods` `ff-coach-log` `ff-coach-thread` `ff-acks` `ff-metrics` `ff-plan`

All written through `saveKey`/`loadKey`, which are guarded — a missing key throws
on first run rather than returning null, so every read is wrapped.

### Things that took several attempts, so please don't "simplify" them

- **Movement matching** (`matchExercise`) — three stages: exact, then *most
  specific* containment, then token overlap. The specificity sort exists because
  "Romanian Deadlifts" was resolving to plain "Deadlift".
- **Contrast tokens** — each accent has three roles: `x` (white text on it),
  `xDim` (pastel tile), `xInk` (text *on* that tile). Collapsing these back into
  one colour per accent breaks WCAG in about eleven places. There's a test.
- **Radial nav geometry** — spread is `asin`-derived, not `acos`. Petals sit on
  arcs measured from vertical, so `x = r·sin(offset)`.
- **Rep rounding** — rounds to 5s above 20 reps, 2s below. Rounding everything
  to 5s made Easy and Moderate collapse onto the same number.
- **Duration variance is proportional** (±8%), not a fixed offset. A fixed
  ±0.8 min is invisible at 15 minutes and swamps a 5-minute session.

---

## 3. Everyday

### Design constraint
No equipment appears anywhere, **including the scale-up options**. A chair, wall
or step is fine; a dumbbell is not. There's a test that greps all 125 entries.

### Three modes, three lengths
`workout` (63 movements, 7 session shapes, 4 tempos) · `stretch` (27 held
stretches) · `flow` (35 poses).

Each mode generates at **5, 10 or 15 minutes**, and the choice is remembered
(`lastDuration` in prefs). Shorter sessions use **fewer movements**, not just
fewer rounds — four exercises in five minutes is one round of each, which is a
warm-up rather than a workout. Verified across 150 sessions per length: every
combination lands within two minutes of its target.

Two things in the flow builder are easy to break:
- Savasana is held **out of the shuffle pool** and only appended deliberately,
  so a five-minute flow can't randomly spend a fifth of itself lying down.
- Its cost is **reserved from the budget up front**. Filling the time and then
  appending the closer pushed every flow a minute over.

### Inclusivity is a data property, not copy
Every movement declares:

- `impact` — `none` / `low` / `high`
- `position` — standing, floor, seated, supported, prone, supine
- `seated` — a real chair-based alternative, where one exists

Three preferences (**no jumping**, **no floor work**, **seated only**) filter the
pool before generation. Verified across 200 sessions each: zero leaks, and
seated-only never produces an empty session. If you add movements, fill these
fields in or the filters silently under-serve people.

### Per-entry detail
All 63 movements carry: cues, muscles worked, breathing guidance, the single most
common mistake, an easier version, a harder version, and a seated version where
applicable. Stretches and poses carry cues plus both scales.

### Storage keys
`bw-log` `bw-prefs` `bw-favs`

---

## 4. Testing

Run from the working directory. All should exit clean.

```bash
python3 validate.py          # FitForge structure — 157 checks
python3 contrast_test.py     # WCAG AA on every colour pairing — 17
node run_tests.js            # FitForge data integrity — 33
node bw_test.js              # Everyday generator + data — 22
node sound_test.js           # audio engine, stubbed Web Audio — 10
node breath_test.js          # breath patterns and guides — 12
node load_test.js            # 1RM percentages, plate rounding — 20
node diff_test.js            # difficulty scaling — 15
node gen_test.js             # workout variety — 4
node picks_test.js           # daily discovery — 11
node wu_test.js              # warm-ups, swap suggestions — 12
node nav_test.js             # radial nav geometry
```

The harnesses collect **every** failure rather than stopping at the first, which
matters when one edit breaks several things at once.

They have caught real bugs repeatedly: a deleted store block, a glossary button
with no sheet mounted, breath phases classified as holds when the lungs were
moving, time-based PRs never being recorded, the coach not seeing your equipment.
Keep running them.

Note they also produce false positives when the checker itself is wrong — the
bracket counter can't parse regex literals, and one equipment check matched
"rack" inside "track over the toes". Read a failure before believing it.

---

## 5. Deploying

Both projects are identical in shape:

```bash
cd fitforge-pwa    # or everyday-pwa
npm install
npm run build      # → dist/
```

`dist/` is static. Netlify, Vercel, GitHub Pages all work. `base` is `./` so
subdirectories are fine.

**Must be HTTPS** or the service worker won't register and offline breaks.

**iOS install:** open in Safari → Share → Add to Home Screen. Chrome on iOS
cannot do this; it has to be Safari.

Icons are generated and included in both. Regenerate with `make_icons.py`.

`src/storage.js` shims `window.storage` onto `localStorage`, so the same
`App.jsx` runs unmodified in the artifact sandbox and in a browser.

---

## 6. Known limits — read before promising anything

**Data is device-local.** No account, no sync, no backup unless the user exports.
Clearing browser data wipes everything. FitForge has CSV + JSON export; Everyday
does not yet.

**The coach needs a connection** and only exists in FitForge. With no logged
history it correctly says so rather than inventing advice.

**No video.** Movement pages link to a YouTube *search* rather than a stored
video ID, because 135 hardcoded IDs would rot. If you want curated videos, buy or
verify them and store IDs as an override layer — the search stays as fallback.

**No stock imagery is bundled.** Everything visual is generated. If you add
photos or video, licence them properly; app distribution rights are narrower than
web use, and "editorial use only" assets don't cover it.

**Twelve screens in FitForge is a lot.** Watch whether testers ever find Plan,
Saved, Tools, or Search. If they don't, cut them rather than adding signposts.

**Not medical advice, and not a substitute for a coach who can see you.** The
safety copy around breathwork, pain and injury is deliberate. If you add
protocols involving breath holds or hyperventilation, gate them the way Wim Hof
and CO₂ Tolerance are gated.

---

## 7. What I'd do next

**FitForge**
1. Structured scores throughout — the runner produces them, the rest of the app
   doesn't consume them yet, so PR comparison is still weaker than it could be.
2. Error boundary. One malformed coach response currently white-screens the app.
3. Plate calculator — it tells you 122 kg but not what to load.
4. Real strength progression (5/3/1 or linear) carried week to week.

**Everyday**
1. Export, matching FitForge's.
2. A weekly rhythm — it's entirely session-by-session right now.
3. More seated movements. Eleven of sixty-three is enough to build a session but
   thin; twenty would be comfortable.
4. A 20- and 30-minute option, if testers ask for it. The builders already solve
   for an arbitrary target, so it's a one-line addition to `DURATIONS`.

**Both**
1. Get them on real phones this week and watch people use them without help.
   Most of what's left to learn won't come from more building.

---

## 7a. Build it (talk to build) — added after the original handoff

A thirteenth screen (`talk`, outer ring, plus a card on Home). Text or voice in,
workout out. Parsing and building are in `src/talk.js`, pure and injected with
the library (`TALK_LIB`, exported from `App.jsx`), so `App.jsx` is no longer the
only source file — keep that in mind if you paste it back into an artifact.

How a request is read, in order — the order matters, each pass claims its words:
movement names (longest alias first, so "back squat" beats "squat" and "dead
bugs" isn't a dead core) → sore/injured body parts (so "easy on my knee" isn't
"easy") → recovery asks → follow-up modifiers → minutes → intensity → format and
goal → blanket exclusions (no jumping, no overhead, no floor) → equipment →
focus. Negation is read from the words before a mention within its clause, and
carries across simple lists ("no burpees, lunges or box jumps").

Things that are deliberate:
- **Refinements keep the workout.** "Harder", "shorter", "swap X for Y" reuse the
  previous format and movements (`ctx.keep`); a new focus, new kit, a new format
  or "shuffle" builds a fresh one.
- **A bench or rack alone doesn't unlock barbell lifts** (`isUsable`). The
  library lists equipment as alternatives, and the generator treats a bench as
  enough for Decline Bench Press. The talk builder doesn't; the generator still does.
- **Chippers water-fill time** and repeat for 2–3 rounds rather than prescribing
  100 muscle-ups. Technical moves cap at 25, everything else at 50.
- **Red flags stop the builder.** Don't soften that copy.

Not done: the AI coach path in this screen has the same limit as everywhere else
(needs the Claude sandbox or a proxy). No e2e test is committed for the screen —
it was driven in Chromium at 390px with faked speech recognition during the build.

## 8. File map

```
FitForge.jsx                  FitForge, single file
fitforge-pwa/
  index.html                  entry + service worker registration
  package.json  vite.config.js  tailwind.config.js  postcss.config.js
  make_icons.py               regenerates all launcher icons
  public/manifest.json  public/service-worker.js  public/*.png
  src/App.jsx                 copy of FitForge.jsx — keep in sync
  src/main.jsx  src/storage.js  src/index.css

BodyweightApp.jsx             Everyday, single file
everyday-pwa/                 same shape as above

design-directions.html        five commercial directions, side by side
animation-options.html        seven icon-animation options
```

When you edit `FitForge.jsx` or `BodyweightApp.jsx`, copy it into the matching
`src/App.jsx`. They will drift otherwise, and the drift is silent.
