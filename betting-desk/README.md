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
  services/sharpness.py   which books predict the close; consensus weights
  services/calibration.py reliability, Brier, CLV by sport/market/edge
  services/thresholds.py  suggested min edge per sport/market; adopted ones
  demo_adaptive.py    synthetic history + tracked plays for the adaptive model
  main.py             FastAPI routes
  demo.py             frozen fixtures so you can run with no key
static/index.html     the UI
tests/                run with `python3 -m pytest tests/` (see Tests below)
data/                 line-movement snapshots (SQLite, gitignored, created on first live refresh)
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

With `weighting=sharp` (or `FAIR_WEIGHTING=sharp`), step 2 becomes a
**weighted consensus**: each book counts in proportion to how well its prices
have predicted the closing line in that sport and market. See
[Adaptive model](#adaptive-model). Off by default; with thin history it is
the plain average anyway.

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
| `GET /api/sharpness/{league}?market=` | free | which books' prices best predicted the close, and the consensus weights that implies |
| `POST /api/calibration` (`GET` in demo) | free | calibration (reliability, Brier) and CLV by sport/market/edge for your tracked plays |
| `POST /api/thresholds/suggest` | free | suggested min edge per sport/market from your plays' CLV. Never applied by itself |
| `GET /api/thresholds` | free | the thresholds you adopted |
| `PUT /api/thresholds/{league}/{market}` | free | adopt one: `{"min_edge": 0.015, "note": "..."}` |
| `DELETE /api/thresholds/{league}/{market}` | free | drop one |
| `GET /api/convert` | free | odds converter |
| `GET /api/usage` | free | credits left |

Leagues: `mlb nfl ncaaf nba wnba ncaab nhl epl mls`. An unknown league is a `400`.
Demo fixtures: `mlb nfl ncaaf nba nhl`.

### Board params

| Param | Default | |
|---|---|---|
| `markets` | `h2h,spreads,totals` | any subset. Anything else is a `400` pointing at `/api/props`. Every game still carries all three keys, and ones you didn't ask for are `null` |
| `books` | all US books | book keys for the Odds API call |
| `stake`, `method`, `min_edge` | env defaults | as before. A bad `method` is a `400` |
| `weighting` | `FAIR_WEIGHTING` (`equal`) | `equal` or `sharp`: weight the consensus by book sharpness. Anything else is a `400` |
| `edge_profile` | `none` (`adopted` if `APPLY_ADOPTED_THRESHOLDS=1`) | `adopted` uses your adopted per-market min edge in place of `min_edge` for those markets |

These are **additive** fields, and nothing existing was removed or renamed:

- `games[].odds_id` is the Odds API event id to hand to `/api/props`
- `games[].markets.{spreads,totals}.off_line_books` lists books dealing a
  different number from the main line: `{book: {point, prices}}`
- `games[].markets.*.sides[].push_possible` and `plays[].push_possible`
- `plays[].odds_id`, plus `markets` at the top level
- `history`: `{recorded, enabled, error?}`, how many price changes this
  refresh wrote to the line-movement store (pass 2)
- `weighting`: `{mode, weighted_markets, weights: {market: {book: w}}, source?,
  thin_markets?: {market: reason}}`. `fair_source` reads
  `weighted consensus:N books` where weights applied (round 3)
- `edge_profile`: `{mode, min_edges: {market: edge}, default_min_edge}`; with
  `edge_profile=adopted`, `plays[]` also carry `min_edge_applied` (round 3)
- `sides[].your_books[]` gained `fair_prob`, `fair_price`, `fair_source`: the
  leave-one-out fair that entry was graded against. `ev_per_dollar`,
  `ev_dollars` and `kelly` can now be `null` (see the fair-value section).
  `plays[].fair_*` now come from the same leave-one-out fair (pass 2)

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
nothing. There is no background poller, because polling spends credits; the
history is exactly as fine-grained as your refreshes. Set `RECORD_HISTORY=0`
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

## Adaptive model

Three things the desk learns from what it has recorded, instead of fixed
settings. All of it is free (it reads data already fetched), all of it is
explainable through a route, and none of it changes the board unless you ask.

### Book sharpness: `GET /api/sharpness/{league}`

Question: which books' prices best predict the closing line, per sport and
market? For every game in the history store that has started:

1. Each book's quote is rebuilt at every moment something moved, up to the
   start. The **close** is the last pre-start state, on the main line.
2. The target for book *B* is the de-vigged consensus of the **other** books
   at the close (leave-one-out), so no book gets credit for agreeing with
   itself. Needs 2+ other books.
3. At every pre-start state where *B* quoted the closing line, *B*'s
   de-vigged probabilities are scored with **KL divergence** against that
   target: log-loss against the close minus the close's own entropy, so a
   50/50 market and a 90/10 market are on the same footing. Averaged per
   game, then across games. Lower is sharper.

Weights: for independent noisy estimates the best blend weights each by
1/variance, and for small errors KL is proportional to squared error, so a
book's **raw weight** is `(1/KL) / mean(1/KL)` across scored books. It is
then **shrunk toward 1** by `n / (n + SHARP_PRIOR_EVENTS)` (n = the book's
scored games) and **clipped** to `[SHARP_WEIGHT_MIN, SHARP_WEIGHT_MAX]`. The
board's consensus becomes the weighted mean of de-vigged probabilities,
renormalised; the graded book is still left out of its own fair price.

**Fallback.** A market is weighted only when it has `SHARP_MIN_EVENTS`
closed games (default 20) and 3+ books with `SHARP_MIN_BOOK_EVENTS` (5) scored
games each. Otherwise its status is `thin`, with a `reason`, and the board
uses the plain consensus. A sharp book (`SHARP_BOOK`) still takes priority,
and 1-2 book markets are unchanged.

Response, per market: `status` (`ok` | `thin`), `events`, `reason`,
`weights` (`null` unless ok), and `books[]` with `rank`, `events`,
`observations`, `mean_kl`, `mean_abs_err` and `mean_bias` (on the home /
Over side; > 0 means the book had it too likely), `raw_weight`, `weight`.
Plus `config` (every knob in force) and `metric`. Params: `market` (any of
`h2h,spreads,totals`), `method`. Results are cached for
`SHARPNESS_CACHE_TTL` seconds (600).

| Env | Default | |
|---|---|---|
| `FAIR_WEIGHTING` | `equal` | board default; `sharp` turns weighting on without the query param |
| `SHARP_MIN_EVENTS` | 20 | closed games in a market before weights apply |
| `SHARP_MIN_BOOK_EVENTS` | 5 | games before one book's score counts |
| `SHARP_PRIOR_EVENTS` | 20 | shrinkage strength toward equal weight |
| `SHARP_WEIGHT_MIN` / `MAX` | 0.25 / 4 | clip |

The history store only fills from your own refreshes, so live weights take
a few weeks of normal use to appear. Game markets only; props aren't scored.

### Calibration and CLV: `POST /api/calibration`

The tracker lives in the browser, so you send the plays and nothing is
stored. Body: `{"plays": [...], "bins": 10}` (up to 5000 plays). Each play is
either flat:

```json
{"league": "nfl", "market": "spreads", "book": "fanduel", "price": -105,
 "stake": 10, "point": -3.5, "fair_prob": 0.53, "ev_per_dollar": 0.035,
 "result": "won", "close_fair_prob": 0.545, "close_point": -3.5}
```

or the tracker's own `localStorage` object as-is (`close: {price, point,
fair_prob}`, `outcome`, `result: {result}` are read). `price` is required;
`ev_per_dollar` is derived from `fair_prob` when missing; a `close_point`
different from `point` means no CLV (different bet).

Response:

- `status`: `ok`, `not_enough_data` (fewer than 30 graded plays, numbers
  still shown with a `note`) or `no_plays`
- `entry` and `close`: `n`, `brier`, `log_loss`, `base_rate`,
  `brier_base_rate` (always forecasting the observed win rate),
  `brier_skill`, `mean_pred`, `ece`, and `reliability[]` buckets with `n`,
  `mean_pred`, `observed`, a 95% Wilson `ci95`, `gap`, `inside_ci` and
  `status` (`thin` under 10 plays). `entry` scores the tool's fair price when
  you logged; `close` scores the closing fair price on the same plays, the
  benchmark to beat. Pushes and ungraded plays are left out.
- `clv`: `overall`, `by_league`, `by_market`, `by_league_market`, `by_edge`
  (`<0%`, `0-1%`, `1-2%`, `2-3%`, `3-5%`, `5%+` of entry EV). Each has
  `plays`, `with_clv`, `mean_clv`, `clv_se`, `beat_close_pct`,
  `mean_entry_ev`, `edge_realisation` (mean CLV / mean entry EV: how much of
  the shown edge survived), `record`, `profit`, `roi`.

`GET /api/calibration` returns the synthetic report in demo mode and
`no_plays` live.

### Suggested thresholds: `POST /api/thresholds/suggest`

Same body as calibration (plus optional `min_plays`). Per (league, market):

1. Fit `CLV = a + b × entry_edge` by least squares over plays with a close.
   `b` is how much of a shown edge survives; `a` the offset.
2. The **break-even edge** is `-a/b`: below it the marginal play is expected
   to lose to the close. Rounded **up** to the 0.5% grid (0-8%).
3. The plays that threshold keeps must beat the close with one-sided 90%
   confidence (mean CLV − 1.28 SE > 0, 20+ plays).

Statuses: `suggested`, `not_enough_data` (fewer than 40 plays with a close;
`needed` says how many more), `edge_not_predictive` (slope not clearly > 0:
bigger shown edges aren't better bets here), `no_threshold_beats_close`.
Each comes with a `reason`, the `fit` (`intercept`, `slope`, `slope_se`,
`break_even_edge`, `break_even_se`) and the full `curve` (per threshold:
`plays`, `mean_clv`, `clv_se`, `clv_lower`, `qualifies`, `graded`, `roi`).
CLV is the criterion, not profit, because it reads in tens of plays where
results need thousands.

**Nothing is applied automatically.** To use a suggestion:

1. `PUT /api/thresholds/nfl/spreads` with `{"min_edge": 0.015}`. Stored in
   `data/thresholds.json` (`THRESHOLDS_FILE`); in demo mode in memory only.
2. Ask the board for `edge_profile=adopted`. Adopted markets use their own
   min edge; the rest use `min_edge`. `APPLY_ADOPTED_THRESHOLDS=1` makes
   `adopted` the default, and `edge_profile=none` still overrides it.

### Demo mode

Everything above works with `DEMO_MODE=1`, on fixtures labelled `DEMO
FIXTURE (synthetic adaptive-model data)`:

- **Sharpness**: 30 finished games per demo league, all three markets. Each
  book's price is the close plus noise of a fixed size per book
  (`demo_adaptive.BOOK_NOISE`: DraftKings tightest, Caesars loosest), so the
  ranking is known and the tests check the model recovers it.
  `/api/board/nfl?weighting=sharp` shows the weighted fair price.
- **Calibration and thresholds**: ~680 tracked plays. The entry fair price
  overstates edges by a per-league amount (`EDGE_OVERSTATEMENT`), so CLV is
  negative on small shown edges and positive on big ones, and NHL needs a
  bigger edge than NFL. Some markets are deliberately too small and come back
  `not_enough_data`.

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

1. **Server-side closing lines.** The Tracked tab captures closes from your
   own refreshes; a scheduled snapshot just before each start would make them
   exact.
2. **Scheduled snapshots.** Line history is recorded on every refresh (see
   `/api/history`). A cron that refreshes on a schedule would give steam moves
   without clicking, at a credit cost per run.
3. **Deploy it.** Railway or Render, free tier, keeps the key server-side.

---

## Tests

```bash
pip install -r requirements-dev.txt
python3 -m pytest tests/     # everything, offline, no key
python3 tests/test_math.py   # 46 assertions on the arithmetic (still runs standalone)
python3 tests/test_e2e.py    # board build against Odds-API-shaped fixtures
```

All of it runs offline. The math tests include known-value checks: two -110
legs price to +264, a -110/-110 market holds 4.545%, and EV at the breakeven
probability is exactly zero. `tests/test_markets.py` covers every demo league
through the real routes, main-line selection, prop pairing, the props opt-in
and dry run, and the cache saving credits. `tests/test_fair_price.py` proves the
consensus and leave-one-out math, multi-way and one-sided markets;
`tests/test_history.py` covers the snapshot store and `/api/history`;
`tests/test_adaptive.py` proves the sharpness, weighting, calibration and
threshold math on hand-worked inputs and covers their routes;
`tests/test_api_validation.py` covers bad input on the free routes.
`tests/test_robustness.py` mocks ESPN and The Odds API with respx and covers
timeouts, connection errors, 401/429/5xx, non-JSON bodies, wrong top-level
shapes, junk rows, stale-cache fallback and per-market failure isolation.

CI: `.github/workflows/betting-desk-tests.yml` runs the suite on Python 3.11
and 3.12 for every PR that touches `betting-desk/`.
