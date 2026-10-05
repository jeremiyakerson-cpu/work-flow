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

Demo mode also covers **NFL, NCAAF (CFB), NBA and NHL** plus player props for
one game in each league. Those fixtures are **synthetic**. The matchups and
prices were written by hand to exercise the code (a book off the main line,
whole-number lines, one-sided props, one outlier price per league), and every
response's `source` says so. Leagues with no fixture (WNBA, CBB, Soccer) return
an empty board with a `note`.

When you're ready for live data, get a key at <https://the-odds-api.com>
(free tier is 500 credits a month), put it in `.env` as `ODDS_API_KEY`, and set
`DEMO_MODE=0`.

API explorer lives at <http://127.0.0.1:8000/docs>.

Want it to keep itself current? Set `AUTO_REFRESH=1` (see
[Keeping it live](#keeping-it-live-auto-refresh-and-alerts)). Want it on a
URL? See [Deploy it](#deploy-it).

---

## What each piece does

```
app/
  math_engine.py      pure arithmetic. 46 unit tests. no network, no opinions
  sources/espn.py     free schedules, scores, standings, injuries
  sources/odds_api.py The Odds API client, TTL cache, credit tracking
  services/board.py   joins the two feeds, runs the math, surfaces plays
  services/props.py   pairs and de-vigs per-event markets (props, periods, alts)
  services/history.py line-movement snapshots (SQLite) and price history
  scheduler.py        optional auto-refresh: cadence by time-to-game, hard daily credit budget
  alerts.py           edge-threshold and line-move alerts, evaluated on every board
  main.py             FastAPI routes, password gate
  demo.py             frozen fixtures so you can run with no key
static/index.html     the UI
tests/                run with `python3 -m pytest tests/` (see Tests below)
data/                 history, alerts, scheduler spend (gitignored, created on first use)
Dockerfile            the deployable image; ../render.yaml and railway.json point at it
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
- **Budget meter** (under it): the auto-refresh's credits spent today against
  its daily budget, or "auto-refresh off". Tap it for the Alerts tab.
- **Alerts tab**: thresholds, browser notifications, the scheduler's state
  (next game, cadence, next run, recent runs, pause, run now) and the alert
  history. New alerts also pop up as toasts on any tab.
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

#### How the consensus is built, and why the graded book is left out

Each book's prices are de-vigged on their own (`DEVIG_METHOD`), the no-vig
probabilities are averaged outcome by outcome, and the average is
renormalised to sum to 1. That works the same for two outcomes, three (a
result with a draw), or N (a scorer list). Only books quoting *every* outcome
of a market, on the same line, go in.

There are two fair prices in every market, on purpose:

| Field | Built from | Answers |
|---|---|---|
| `sides[].fair_prob` / `fair_price` / market `fair_source` | every book (or the sharp) | what is this outcome worth? This is what the board shows |
| `sides[].your_books[].fair_prob` / `fair_source`, and every `plays[]` row | every book **except the one being graded** | is *this* price a good bet? EV and Kelly come from this one |

Up to pass 1 the code graded each of your books against a consensus that
included that book's own price. That was a bug, not a design choice: it pulls
the fair price toward the graded book, so every edge (and every overlay you
could have taken) is shrunk by roughly 1/N of the gap, and with two books it
is 50% the book grading itself. It also contradicted this README and
`math_engine.find_edges`, which already left the book out.

Consequences worth knowing:

- The graded fair's `fair_source` says what was left out, e.g.
  `consensus:3 books (excl. fanduel)`. With only one other book it is
  `single book:caesars (excl. fanduel)`.
- If your book is the only one quoting a market, there is no independent fair.
  Its EV, Kelly and fair fields are `null` and it never becomes a play.
- `best_ev_per_dollar` grades the best price with the best book left out.
- If `SHARP_BOOK` is set and you grade the sharp book itself, it is graded
  against the consensus of everyone else.
- **One-sided props.** If your book hangs only the Over (or only Yes), that
  price can't be de-vigged by itself, but it is still a bet. When other books
  quote the full market, your one-sided price is graded against their
  consensus and marked `one_sided: true` (its `hold` is `null`). If no book
  quotes both sides, there is no fair price at all and the row carries a `note`.
- Additive de-vig can produce a negative probability on a long shot in a
  heavily vigged multi-way market. That market is reported with no fair price
  rather than a nonsense one.

---

## Keeping it live: auto-refresh and alerts

### Auto-refresh (off by default)

`AUTO_REFRESH=1` starts a background thread that snapshots the board for each
league in `AUTO_REFRESH_LEAGUES`. A scheduled snapshot is the same board a
Refresh builds: it writes line history and runs alerts.

| Time to the next start | Snapshot every |
|---|---|
| more than 24h | 6 hours |
| 6 to 24h | 2 hours |
| 2 to 6h | 45 min |
| 30 min to 2h | 15 min |
| under 30 min | 10 min |
| no game in the next 7 days | never |

Change the tiers with `AUTO_REFRESH_CADENCE=24h=360,6h=120,2h=45,30m=15,0=10`.
Start times come from The Odds API's `/events`, which is free.

The credit rules are the point:

- **Hard daily budget**, `AUTO_REFRESH_DAILY_CREDITS` (default 15, about a
  30th of the free tier). A run that would go over is skipped, not trimmed.
  It resets at 00:00 UTC. The *actual* cost from the API's headers is what
  gets booked, so a cache hit costs 0.
- **The close gets priority.** Runs more than `AUTO_REFRESH_FAR_HOURS` (3)
  before the next game may use only `AUTO_REFRESH_FAR_SHARE` (half) of the
  day's budget. The rest is kept for the hours before games, which is when a
  snapshot doubles as a closing line.
- **One run is always left for the close.** Until a game is within
  `AUTO_REFRESH_CLOSE_MINUTES` (30) of starting, runs leave one run's worth
  of budget unspent, so the snapshot just before the start is never the one
  that gets skipped.
- **Monthly floor.** At `AUTO_REFRESH_RESERVE` (100) credits left in the
  month it stops, so your own refreshes still work.
- Spend and the run log persist in `data/scheduler.json` across restarts.

With the defaults, an NFL Sunday with a 17:00 UTC (1pm ET) kickoff runs at
00:00 and 02:00 (the far-from-game half of the budget), then 14:00 and 14:45,
holds back, and spends the last 3 credits at 16:30, 30 minutes before kickoff.
That's 15 credits, at 3 per 3-market board. The budget is per day, not per
game, so the 4pm and night games get no scheduled snapshots unless you raise
it. That trade is deliberate.

The page picks up scheduled snapshots without spending. When it sees a newer
run for the league on screen, it reloads the board with `cached_only=1`, which
reprices the scheduler's last fetch and never calls the API.

Run **one** server worker: every worker would start its own scheduler and
spend its own budget. `GET /api/scheduler` shows the state, and
`POST /api/scheduler/pause`, `/resume` and `/run/{league}` drive it. Run now
still goes through the budget.

### Alerts

Every board the server builds, yours or the scheduler's, is compared with the
previous one for that league:

- **Edge**: one of your books' prices reaches `min_edge` EV (default 3%) and
  wasn't there on the last board. It alerts once. If it drops below and comes
  back, it alerts again.
- **Move**: a market's fair probability moves `move_prob` or more (default
  3 points) between boards, or a spread or total's main line moves
  `move_points` or more (default 1). Named after the side that got more
  likely.

A board with stale or failed odds is ignored, so a feed outage doesn't re-fire
every alert when the feed recovers. Finished games never alert. The defaults
come from `ALERT_MIN_EDGE`, `ALERT_MOVE_PROB` and `ALERT_MOVE_POINTS`, and the
Alerts tab changes them for everyone using the server. "Use board filter"
copies your Min edge filter across.

In the browser, new alerts show as toasts. Switch on **Browser
notifications** in the Alerts tab and you also get system notifications while
the page is open in a tab, even a background one. Notifications need
`localhost` or HTTPS. History is kept in `data/alerts.json` (last 500) and
only in memory in demo mode.

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
  (`usage.cache_hits` counts them)
- Scope to `bookmakers` instead of `regions` where you can
- The UI shows `credits_remaining` after every refresh
- **Props are opt-in.** Live calls to `/api/props` and `/api/markets` return
  `403` until you set `ENABLE_PROPS=1`. Demo mode ignores the flag.
- `PROPS_MAX_MARKETS` (default 5) caps the markets one props call can ask for.
- `dry_run=true` on either props route returns `estimated_cost` without making
  the call. It's an upper bound, because the API bills only markets that come
  back with data.
- Leaving out `markets` gets a **short default list** (two markets per league,
  so 2 credits) rather than every prop market.

Pull props per game, when you're actually looking at that game. Not across a
slate out of curiosity.

With `books` blank, props are priced by region (`us`), not by your two books.
That costs the same, since up to ten books bill as one region, and it gives
the consensus every US book instead of just the two you're grading.

---

## Endpoints

| Route | Cost | What |
|---|---|---|
| `GET /api/sports` | free | leagues, their markets, demo coverage, whether props are on |
| `GET /api/schedule/{league}` | free | ESPN slate. `date=YYYY-MM-DD`; football also takes `week=` |
| `GET /api/standings/{league}` | free | records |
| `GET /api/injuries/{league}` | free | injury report |
| `GET /api/board/{league}` | credits | the refresh button |
| `GET /api/board?sport=` | credits | same thing, league as a query param |
| `GET /api/events/{league}` | free | Odds API event ids, which the props routes need |
| `GET /api/props/{league}/{event_id}` | credits, opt-in | player props, de-vigged, with plays |
| `GET /api/props?sport=&event_id=` | credits, opt-in | same thing, as query params |
| `GET /api/markets/{league}/{event_id}?markets=` | credits, opt-in | any per-event market (e.g. `totals_h1`, `alternate_spreads`) |
| `GET /api/history/{league}/{event_id}?market=` | free | line movement for one market: every price the desk has stored, per book, and the fair price over time |
| `POST /api/parlay` | free | price a slip, EV, singles comparison |
| `POST /api/results` | free | grade tracked plays from ESPN final scores |
| `GET /api/convert` | free | odds converter |
| `GET /api/usage` | free | credits left |
| `GET /api/alerts?after=&limit=` | free | alert history, newest first, plus `unread`, `latest_id`, `config` |
| `POST /api/alerts/config` | free | `{enabled, min_edge, move_prob, move_points}`, fractions (0.03 = 3%) |
| `POST /api/alerts/read` | free | `{upto}` marks alerts read (all if omitted) |
| `DELETE /api/alerts` | free | clear the history |
| `GET /api/scheduler` | free | auto-refresh: enabled, budget, per-league next game / cadence / next run, recent runs |
| `POST /api/scheduler/pause` · `/resume` | free | |
| `POST /api/scheduler/run/{league}` | credits | snapshot now, within the daily budget (`429` if over, `409` if auto-refresh is off) |
| `GET /healthz` | free | `{"ok": true}`; the only route open when `DESK_PASSWORD` is set |

Leagues: `mlb nfl ncaaf nba wnba ncaab nhl epl mls`. An unknown league is a `400`.
Demo fixtures: `mlb nfl ncaaf nba nhl`.

### Board params

| Param | Default | |
|---|---|---|
| `markets` | `h2h,spreads,totals` | any subset. Anything else is a `400` pointing at `/api/props`. Every game still carries all three keys, and ones you didn't ask for are `null` |
| `books` | all US books | book keys for the Odds API call |
| `stake`, `method`, `min_edge` | env defaults | as before. A bad `method` is a `400` |
| `cached_only` | `false` | never spend: reprice the last response fetched for these markets and books, however old. Nothing cached yet is an `odds` error of kind `cache_miss` |

These are **additive** fields, and nothing existing was removed or renamed:

- `games[].odds_id` is the Odds API event id to hand to `/api/props`
- `games[].markets.{spreads,totals}.off_line_books` lists books dealing a
  different number from the main line: `{book: {point, prices}}`
- `games[].markets.*.sides[].push_possible` and `plays[].push_possible`
- `plays[].odds_id`, plus `markets` at the top level
- `history`: `{recorded, enabled, error?}`, how many price changes this
  refresh wrote to the line-movement store (pass 2)
- `sides[].your_books[]` gained `fair_prob`, `fair_price`, `fair_source`: the
  leave-one-out fair that entry was graded against. `ev_per_dollar`,
  `ev_dollars` and `kelly` can now be `null` (see the fair-value section).
  `plays[].fair_*` now come from the same leave-one-out fair (pass 2)
- `alerts`: the alerts this board raised (empty with `cached_only`),
  `odds_fetched_at` (when the prices were actually fetched; older than
  `generated_at` on a cache hit), `cached_only`, and `origin`
  (`manual` or `scheduled`) (round 3)

### Props / per-event params

`markets` (props: optional, defaults to the league's short list; `/api/markets`:
required), `books`, `stake`, `min_edge`, `method`, `dry_run`.

Response: `event`, `props[]`, `plays[]` (sorted by EV, same fields as board
plays plus `player`), `summary`, `estimated_cost`, `usage`, `source`. Each
`props[]` row has:

- `market`, `player` (`null` for team markets), `point`, and `labels`, the
  sides in order: `["Over","Under"]`, `["Yes","No"]`, `["Team -3.5","Team +3.5"]`,
  or three or more names for a multi-way market (`["Away","Draw","Home"]`)
- `outcomes`: `{label: {book: price}}`. Every quoted price is here, including
  books that quote only one side
- `analysis`: the same shape as a board market, or `null` with a `note` when no
  book quotes every side. Your book's one-sided prices show up in
  `your_books` with `one_sided: true`; plays carry `one_sided` too
- `over` / `under`: the original `/api/props` shape, kept on over/under rows

A different line is a different row. Mahomes o262.5 and o259.5 are two bets,
never one averaged number.

### Line movement: `GET /api/history/{league}/{event_id}`

Every live board refresh, and every live props/markets call, writes the prices
it fetched to a SQLite file, `data/history.sqlite3` (gitignored). Only changes
are written: a refresh inside the cache window, or on a quiet market, adds
nothing. Nothing polls unless you switch on auto-refresh, because polling spends
credits; the history is exactly as fine-grained as your refreshes, plus the scheduler's if
you turn it on (next section). Set `RECORD_HISTORY=0`
to turn it off, or `HISTORY_DB=/path/file.sqlite3` to move it. A write failure
(read-only disk, say) is reported in the board's `history.error` and never
fails the board.

| Param | Default | |
|---|---|---|
| `market` | `h2h` | `h2h`, `spreads`, `totals`, or any per-event key you've fetched (`player_points`) |
| `book` | all | one book key |
| `player` | all | props: one player's lines (case-insensitive) |
| `method` | env | de-vig method for the fair series |

Response:

- `event`: `{home, away, start_utc}`; `snapshots`: rows matched
- `series[]`: one per `(book, side, player, point)`, with `points: [{t, price}]`
  oldest first, plus `open`, `current`, `moves`. A new number is a new series:
  a spread going -3 to -3.5 ends one series and starts another
- `fair[]` (game markets only): at every timestamp something moved,
  `{t, line, book_count, fair_source, sides: [{label, fair_prob, fair_price}]}`.
  Each book's quote is rebuilt as of that moment, the main line is chosen the
  same way the board does, and the same de-vig and consensus are applied.
  Books that hadn't quoted yet are absent; nothing is carried back in time.
  Props get a `fair_note` instead
- `recording`, `source`, and a `note` when there's nothing stored yet

In demo mode every demo game has a **synthetic** history (labelled
`DEMO FIXTURE (synthetic line history)`): four snapshots from about four days
out, ending exactly at the fixture's current prices, so the last `fair[]`
point equals the board's fair price. MLB's history is synthetic too, even
though its final prices are the real Sep 15 board. Nothing is written to disk
in demo mode.

---

## Deploy it

The image is `betting-desk/Dockerfile`: Python 3.13 slim, non-root, one
uvicorn worker on `$PORT`. The Odds API key lives only in the host's
environment. The browser only talks to the app, never to The Odds API, so the
key never reaches a page.

**Set `DESK_PASSWORD` on anything with a public URL.** With it set, every
route except `/healthz` needs HTTP Basic auth (any username, that password),
`/docs` included. The browser asks once per session. The comparison is
constant-time. Serve it over HTTPS, which Render and Railway do by default.
Without it, anyone with the URL can spend your credits.

### Render (one click)

[![Deploy to Render](https://render.com/images/deploy-to-render-button.svg)](https://render.com/deploy?repo=https://github.com/jeremiyakerson-cpu/work-flow)

The Blueprint is `render.yaml` at the repo root. It deploys only
`betting-desk/` and only redeploys when that folder changes. It asks for
`ODDS_API_KEY` and **generates a random `DESK_PASSWORD`**, which you can read
or change under Environment in the dashboard.

- Render's **free** plan sleeps after about 15 idle minutes and has no
  persistent disk. The desk works fine there, but auto-refresh only runs
  while it's awake, and history, alerts and today's spend start over on each
  deploy. The spend cap still holds within a run, and a fresh day's budget is
  never more than `AUTO_REFRESH_DAILY_CREDITS`.
- For unattended auto-refresh, switch to an always-on plan, uncomment the
  `disk` block (mounted at `/app/data`), and set `AUTO_REFRESH=1`.

### Railway

New project → Deploy from GitHub repo → this repo. In the service settings,
set **Root Directory** to `betting-desk` and **Config file** to
`/betting-desk/railway.json`, which gives the Dockerfile build, the `/healthz`
check and one replica. Add `ODDS_API_KEY` and `DESK_PASSWORD` as variables,
and add a volume at `/app/data` to keep history and alerts.

### Anywhere with Docker

```bash
cd betting-desk
docker build -t betting-desk .
docker run -p 8000:8000 --env-file .env -v desk-data:/app/data betting-desk
```

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

**Books on a different line are not averaged in.** Prices on -3 and -3.5 are
different bets. Spreads and totals are graded on the main line only, which is
the number most books deal, with ties going to the most balanced market. Books
dealing a different number are listed in `off_line_books`, not dropped
silently.

**Whole-number lines are flagged.** On a -7 or a total of 44 the de-vig gives
win-given-no-push probabilities. The sign of the EV is right, but its size is
overstated by the push chance, which the feed doesn't give us. These lines
carry `push_possible: true`.

**Three-way markets are absent, not wrong, on the board.** A soccer moneyline
with a draw comes back `null` on the board rather than de-vigging two of its
three outcomes. Per-event markets (`/api/markets`, e.g. `h2h_3_way`) de-vig
all outcomes together.

**Games are matched on start time as well as names.** Two feeds' listings more
than 12 hours apart are never joined, even when the names match exactly. That
matters on college slates with two Miamis.

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

1. **Server-side closing lines.** The scheduler already snapshots every 10
   minutes before games. Storing the last pre-start snapshot per event as
   the official close would let the Tracked tab read closes from the server
   instead of your own refreshes.
2. **Per-user alert settings.** Thresholds are server-wide, which is right for
   one person and wrong for a shared desk.
3. **Push that doesn't need an open tab.** Web Push or an email/SMS hook, for
   alerts while every tab is closed.

---

## Tests

```bash
pip install -r requirements-dev.txt
playwright install chromium  # UI smoke test only; or: export BD_CHROMIUM=/path/to/chrome
python3 -m pytest tests/     # everything, offline, no key
python3 tests/test_math.py   # 46 assertions on the arithmetic (still runs standalone)
python3 tests/test_e2e.py    # board build against Odds-API-shaped fixtures
```

`test_ui_smoke.py` starts the app in demo mode on a spare port and drives it
in Chromium at phone width. It loads the board, filters it, tracks a play,
sees it graded in Results and exports CSV. Then it checks the budget meter,
saves an alert threshold, sees an edge alert toast and land in the history,
and runs the scheduler by hand. It skips itself if Playwright or a browser
isn't installed. Everything else needs no network.

All of it runs offline. The math tests include known-value checks: two -110
legs price to +264, a -110/-110 market holds 4.545%, and EV at the breakeven
probability is exactly zero. `tests/test_markets.py` covers every demo league
through the real routes, main-line selection, prop pairing, the props opt-in
and dry run, and the cache saving credits. `tests/test_fair_price.py` proves the
consensus and leave-one-out math, multi-way and one-sided markets;
`tests/test_history.py` covers the snapshot store and `/api/history`;
`tests/test_api_validation.py` covers bad input on the free routes.
`tests/test_robustness.py` mocks ESPN and The Odds API with respx and covers
timeouts, connection errors, 401/429/5xx, non-JSON bodies, wrong top-level
shapes, junk rows, stale-cache fallback and per-market failure isolation.
`tests/test_scheduler.py` walks a fake clock through cadence tiers, the daily
budget and its UTC rollover, the far-from-game cap, the monthly reserve,
booked-vs-estimated cost and restarts. `tests/test_alerts.py` covers edge
crossings, moves, de-duplication, ignored stale boards, persistence and
`cached_only` never spending. `tests/test_access.py` covers the password gate.

Dependencies are pinned in `requirements*.txt`. To refresh, bump a pin, run
the suite with `-W error`, and commit. `httpx2` is there because Starlette's
`TestClient` deprecated plain `httpx`. The app itself still uses `httpx`.

CI: `.github/workflows/betting-desk-tests.yml` runs the suite with warnings as
errors on Python 3.11, 3.12, 3.13 and 3.14, runs the Chromium smoke test on
3.13, and builds the Docker image and probes it behind a password. It runs on
every PR that touches `betting-desk/` or `render.yaml`. Python 3.15 is left
out until the pinned pydantic-core ships wheels for it.
