# Betting Desk

A local research tool. It pulls schedules and context free from ESPN, pulls
odds from The Odds API, strips the vig off every market, and tells you where
FanDuel or DraftKings are pricing something above or below fair value.

It does not place bets. It has no account access. It never will.

---

## Run it in two minutes

```bash
cd betting-desk
cp .env.example .env
# demo mode needs no key at all:
echo "DEMO_MODE=1" >> .env
./run.sh
```

Open <http://127.0.0.1:8000>. Hit **Refresh board**. You'll see three MLB games
with four books apiece, priced off frozen fixture data from Sep 15 2026.

When you're ready for live data, get a key at <https://the-odds-api.com>
(free tier is 500 credits a month), put it in `.env` as `ODDS_API_KEY`, and set
`DEMO_MODE=0`.

API explorer lives at <http://127.0.0.1:8000/docs>.

---

## What each piece does

```
app/
  math_engine.py      pure arithmetic. 46 unit tests. no network, no opinions
  sources/espn.py     free schedules, scores, standings, injuries
  sources/odds_api.py The Odds API client, TTL cache, credit tracking
  services/board.py   joins the two feeds, runs the math, surfaces plays
  main.py             FastAPI routes
  demo.py             frozen fixtures so you can run with no key
static/index.html     the UI
tests/                run with `python3 -m pytest tests/` (see Tests below)
```

---

## Using the UI

- **Filters**: book (best of your books, or one book), market type (built from
  whatever markets the API returns, props included), minimum edge %, and sort.
  Preferences are remembered in the browser.
- **Best play**: the highest-EV price that passes your filters is pinned above
  the board and outlined on it. If nothing is +EV it says so and shows the
  price *closest to fair* instead, labelled as a reference point, not a play.
- **Credits pill** (top right): credits left on The Odds API after the last
  call. Amber under 50, red at zero.
- **Tracked tab**: tap ☆ on any price to log it (stored in `localStorage`,
  this browser only). Every board refresh before the game starts records that
  book's current price; once ESPN says the game is live (or the clock passes
  start time) the last pre-start price becomes the **closing line**, and the
  tab shows CLV:
  - *CLV vs fair* = `fair_close_prob × decimal(your price) − 1`: your price
    against the de-vigged close. This is the number that matters.
  - *Price CLV* = `decimal(yours) / decimal(close) − 1` at the same book.
  - If the point moved (8.5 → 9) the prices aren't comparable and it says so.
  - The close is only as fresh as your last refresh before the start. "Lock
    close now" freezes it manually (demo fixtures never go live, so use it there).
- **Tracked → Results**: grades each play from ESPN final scores (won / lost /
  push) via `POST /api/results`, then shows your record, profit at the stakes
  you logged, ROI, average CLV, a per-play CLV chart with a running average, a
  cumulative-profit chart and a week-by-week table. Only moneyline, spread and
  total grade automatically; props and anything else get **Won / Lost / Push**
  buttons so you grade them by hand. In demo mode the server invents a final
  score per game (stable, labelled synthetic) so you can see the flow.
  **Export CSV** downloads every tracked play with entry, close, CLV and result.
- **Any sport, any market**: the league rail comes from `/api/sports` when the
  backend has it (demo leagues without a fixture are dimmed), and each game
  renders whatever keys are in `markets`. Games with an `odds_id` get a
  **Props** button: in demo it loads straight away; live, the first tap asks
  the server for the credit cost (`dry_run`) and a second tap spends it.
  Off-line books and whole-number lines (push possible) are flagged.

## When a feed fails

`/api/board` never 500s on an upstream problem. It returns 200 with whatever
did load plus an `errors` list (`source`, `kind`, `message`) and
`degraded: true`:

| Failure | What you get |
|---|---|
| Odds API timeout / 5xx / 429 / 401 / non-JSON | ESPN slate without prices; if an older cached response exists it's served with `stale_odds: true` |
| ESPN timeout / 429 / malformed | Odds-only games (named from the odds feed) |
| Both down | Empty board, both errors listed |
| Partial or junk rows | Bad events, books, markets and prices are dropped; the rest is priced |
| One market blows up in the math | That market is `null`, a line goes in `warnings`, the rest of the board stands |
| No `ODDS_API_KEY` | Schedule only, `kind: "config"` |

The UI shows each as a banner above the board and keeps the last good board
on screen if a refresh fails outright.

### The math

- **De-vig**: three methods. Default is **power** (solve for *k* where
  `sum(p**k) = 1`). Multiplicative is the common one but it distorts lopsided
  markets, which is exactly where longshot props live. Switch in the UI and
  watch the fair price move — the difference on a heavy favourite is real.
- **Hold** is `1 - 1/overround`, the number books actually quote. A -110/-110
  market holds **4.545%**, not 4.76%. Those are different quantities and
  conflating them overstates the margin on every market.
- **EV** is reported in dollars at your stake, not just percentages.
- **Kelly** is computed full, displayed quarter. Full Kelly is only correct
  if your probability estimate is exact, which it never is.

### Where "fair value" comes from

In priority order:

1. **A sharp book**, if your feed has one. Set `SHARP_BOOK=pinnacle`. One
   sharp de-vigged line beats an average of soft books. The Odds API does not
   carry Pinnacle; SportsGameOdds ($99/mo) does.
2. **A consensus** of every book in the feed, *excluding the book you're
   grading*, so it isn't marking its own homework. Needs 3+ books.
3. **A single book**, labelled as such so you know the number is weak.

Every market reports which of these it used, in `fair_source`. If it says
`single book:fanduel`, treat the edge as noise.

---

## The cost model — read this before you burn the month

The Odds API charges **markets × regions** credits per call, not one.

| Call | Cost |
|---|---|
| `/sports` | free |
| `/events` | free |
| Game lines, 3 markets, 1 region | 3 credits |
| Game lines, 3 markets, scoped to 2 books | 3 credits |
| Props, 5 markets, one game | 5 credits |
| Props, 5 markets, 15-game slate | **75 credits** |

The free tier is 500 credits a month. That's about six full prop refreshes on
an MLB slate, and nothing else. Two defences are built in:

- `CACHE_TTL` (default 120s) — repeat refreshes inside the window are free
- Scope to `bookmakers` instead of `regions` where you can
- The UI shows `credits_remaining` after every refresh

Pull props per game, when you're actually looking at that game. Not across a
slate out of curiosity.

---

## Endpoints

| Route | Cost | What |
|---|---|---|
| `GET /api/schedule/{league}` | free | ESPN slate |
| `GET /api/standings/{league}` | free | records |
| `GET /api/injuries/{league}` | free | injury report |
| `GET /api/board/{league}` | credits | the refresh button |
| `GET /api/props/{league}/{event_id}` | credits | player props, all books |
| `POST /api/parlay` | free | price a slip, EV, singles comparison |
| `POST /api/results` | free | grade tracked plays from ESPN final scores |
| `GET /api/convert` | free | odds converter |
| `GET /api/usage` | free | credits left |

Leagues: `mlb nfl ncaaf nba wnba ncaab nhl epl mls`

---

## Tests

```bash
pip install -r requirements-dev.txt
playwright install chromium        # or: export BD_CHROMIUM=/path/to/chrome
python3 -m pytest tests/
```

`test_ui_smoke.py` starts the app in demo mode on a spare port and drives it
in Chromium at phone width: load the board, filter it, track a play, see it
graded in Results, export CSV. It skips itself if Playwright or a browser
isn't installed; everything else needs no network.

---

## Design rules the code actually enforces

**A number is either sourced or absent.** If a book doesn't quote a market,
the cell is `null`. Nothing is filled from a neighbouring book, an average,
or a previous refresh. The UI renders a dash.

**EV is never guessed.** `POST /api/parlay` returns `ev_dollars: null` if any
leg lacks a fair probability, with a note saying so. It does not substitute
the book's own implied probability and call the result EV — that always
returns zero and is worse than saying nothing.

**Same-game parlays get flagged.** The independence math overstates them
because same-game legs are correlated. `same_game: true` and a note say so
rather than letting a confident wrong number stand.

**The slip shows what the same money does as singles.** Same total risk,
split across the legs. It is usually the better number, and seeing it next to
the parlay EV is the point.

---

## What this cannot do

- **Hard Rock is not in The Odds API.** You'll be comparing FanDuel and
  DraftKings against a consensus, then deciding for yourself whether Hard
  Rock's number beats it. That last step stays manual.
- No live in-play odds on the free tier.
- No account access at any book. No balance, no placing, no cancelling.
- Edges found here are small and depend entirely on the quality of the fair
  estimate. Without a sharp anchor, a "consensus" of soft books that all copy
  each other is worth less than it looks.

---

## Next things worth building

1. **Server-side closing lines.** The Tracked tab captures closes from your
   own refreshes; a scheduled snapshot just before each start would make them
   exact.
2. **Line-movement history.** Snapshot the board on a schedule, store it, and
   you can see steam moves instead of guessing.
3. **A real database.** SQLite is plenty. Right now nothing persists.
4. **Deploy it.** Railway or Render, free tier, keeps the key server-side.

---

## Tests

```bash
pip install pytest respx
python3 -m pytest tests/     # everything below, offline
python3 tests/test_math.py   # 46 assertions on the arithmetic (also runs standalone)
python3 tests/test_e2e.py    # board build against Odds-API-shaped fixtures
```

`test_robustness.py` mocks ESPN and The Odds API with respx and covers
timeouts, connection errors, 401/429/5xx, non-JSON bodies, wrong top-level
shapes, junk rows, stale-cache fallback and per-market failure isolation.

The math and e2e scripts run offline with no dependencies. The math tests include known-value
checks: two -110 legs price to +264, a -110/-110 market holds 4.545%, and EV
at the breakeven probability is exactly zero.
