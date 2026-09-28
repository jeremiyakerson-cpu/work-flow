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
  main.py             FastAPI routes
  demo.py             frozen fixtures so you can run with no key
static/index.html     the UI
tests/                run with `python3 -m pytest tests/`
```

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
| `POST /api/parlay` | free | price a slip, EV, singles comparison |
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

These are **additive** fields, and nothing existing was removed or renamed:

- `games[].odds_id` is the Odds API event id to hand to `/api/props`
- `games[].markets.{spreads,totals}.off_line_books` lists books dealing a
  different number from the main line: `{book: {point, prices}}`
- `games[].markets.*.sides[].push_possible` and `plays[].push_possible`
- `plays[].odds_id`, plus `markets` at the top level

### Props / per-event params

`markets` (props: optional, defaults to the league's short list; `/api/markets`:
required), `books`, `stake`, `min_edge`, `method`, `dry_run`.

Response: `event`, `props[]`, `plays[]` (sorted by EV, same fields as board
plays plus `player`), `summary`, `estimated_cost`, `usage`, `source`. Each
`props[]` row has:

- `market`, `player` (`null` for team markets), `point`, and `labels`, the two
  sides in order: `["Over","Under"]`, `["Yes","No"]`, or `["Team -3.5","Team +3.5"]`
- `outcomes`: `{label: {book: price}}`. Every quoted price is here, including
  books that quote only one side
- `analysis`: the same shape as a board market, or `null` with a `note` when no
  book quotes both sides
- `over` / `under`: the original `/api/props` shape, kept on over/under rows

A different line is a different row. Mahomes o262.5 and o259.5 are two bets,
never one averaged number.

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

**Three-way markets are absent, not wrong.** A soccer moneyline with a draw
comes back `null` rather than de-vigging two of its three outcomes.

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

1. **Bet log with closing-line value.** Record every bet and the closing
   price, then compare. CLV is the only fast feedback on whether your process
   works — win rate takes hundreds of bets to say anything.
2. **Line-movement history.** Snapshot the board on a schedule, store it, and
   you can see steam moves instead of guessing.
3. **A real database.** SQLite is plenty. Right now nothing persists.
4. **Deploy it.** Railway or Render, free tier, keeps the key server-side.

---

## Tests

```bash
python3 -m pytest tests/     # everything, offline, no key
python3 tests/test_math.py   # 46 assertions on the arithmetic (still runs standalone)
python3 tests/test_e2e.py    # board build against Odds-API-shaped fixtures
```

All of it runs offline. The math tests include known-value checks: two -110
legs price to +264, a -110/-110 market holds 4.545%, and EV at the breakeven
probability is exactly zero. `tests/test_markets.py` covers every demo league
through the real routes, main-line selection, prop pairing, the props opt-in
and dry run, and the cache saving credits.
