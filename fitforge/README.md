# FitForge

Offline-capable training app: WOD generator, 135-movement lift library, cardio
training with a pace metronome and race plans, yoga flows, and warm-up/cool-down
routines. All data is stored locally on the device.

## Build it — say what you want

Home → **Tell me what you want** (or **Build it** in the hub). Type or speak a
request the way you'd say it to a coach:

> 25 minutes, legs and core, kettlebell only, no jumping, my knee's a bit sore

and it builds a workout from the movement library, showing what it understood
(time, intensity, format, focus, kit, what it's leaving out and why) next to the
workout. Follow-ups refine it in place: **harder**, **shorter**, **make it 30
minutes**, **swap the swings for goblet squats**, **no lunges**. **Start this**
loads it into the timer. Wind-down asks ("something to help me sleep", "stretch
my tight hips") open the matching yoga flow or breathing pattern instead.

- **Runs on the device.** `src/talk.js` parses and builds deterministically, with
  no network, so it works offline and in the installed app. The AI coach is an
  optional extra underneath; it only works where the app can reach Claude.
- **Voice** uses the browser's built-in speech recognition (Chrome, Edge,
  Safari). Firefox has none, and installed iOS home-screen apps don't always
  expose it, so there the mic button hides and the keyboard's dictation key does
  the same job. When a request was spoken, the reply is read back.
- **Safety.** Chest pain, dizziness, palpitations or trouble breathing stop the
  builder with advice to get checked instead of a workout. Sore or injured body
  parts remove the movements that load them.

## Test it as a claude.ai artifact

```bash
npm run build:artifact   # -> dist/fitforge-artifact.html, one self-contained page
```

Publish that file as an artifact with the `dictation`, `tts`, `sample` and
`downloads` capabilities. Inside an artifact the app swaps in the platform's
versions of things the frame blocks: dictation for the mic, its voice for
spoken replies, Claude for the AI coach, and the save dialog for exports.
Outside an artifact none of that code runs.

## Tests

```bash
npm test         # parser + builder, against the real movement library (43 tests)
```

`tests/load-lib.mjs` compiles `App.jsx` with esbuild so the tests read the same
`EX` library the app ships, rather than a copy that could drift.

## Run it

```bash
npm install
npm run dev      # http://localhost:5173
```

## Build and deploy

```bash
npm run build    # outputs to dist/
```

`dist/` is a static folder — drop it on Netlify, Vercel, GitHub Pages, or any
static host. `base` is set to `./` so it works from a subdirectory too.

## Installing to a phone

Once served over **HTTPS** (a service worker will not register over plain HTTP):

- **iOS** — open in Safari, Share → *Add to Home Screen*
- **Android** — open in Chrome, menu → *Install app*

It then launches full-screen and works offline.

## Icons

All launcher icons are already generated in `public/` — 192, 512, a maskable
512 with the mark inside the safe zone, plus favicon and apple-touch variants.
They're original line art, not stock.

## Layout

```
index.html              entry point + service worker registration
public/manifest.json    PWA metadata
public/service-worker.js  cache-first offline shell
src/App.jsx             the app (screens, data, timer)
src/talk.js             request parser + workout builder for "Build it"
src/storage.js          localStorage shim for window.storage
src/main.jsx            React mount
```

## Storage

The app reads and writes through `window.storage`. Inside the Claude artifact
sandbox that API is provided for you; in a browser, `src/storage.js` supplies
the same async interface on top of `localStorage`, so `App.jsx` runs unmodified
in both places.

Keys used: `ff-settings`, `equipment-selection`, `ff-history`, `ff-prs`,
`ff-favorites`, `ff-plan`, `ff-talk-recent` (last four "Build it" requests).
