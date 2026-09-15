# Betting Desk

Compares the prices you can actually bet against a no-vig fair estimate, and
tells you the EV and the stake. Pure Python standard library — no pip install,
no build step.

```sh
cp .env.example .env && echo "DEMO_MODE=1" >> .env
./run.sh
# -> http://127.0.0.1:8787
```

`run.sh` runs the unit tests before it serves. The math is the product; a red
suite should never put a price on screen.

```sh
./run.sh test        # tests only
SKIP_TESTS=1 ./run.sh
PORT=9000 ./run.sh   # real env vars override .env
```

## Layout

```
desk/odds.py      all the arithmetic. No I/O, no state, no dependencies.
desk/config.py    .env + environment -> Settings
desk/feed.py      prices in, normalised (demo fixtures | JSON file | odds API)
desk/analyze.py   feed + settings -> ranked edges, parlay grading, staking
desk/server.py    stdlib HTTP, JSON API, serves the UI
static/index.html the desk
tests/            144 tests, stdlib unittest
```

`odds.py` is deliberately the only file with math in it. Everything above it
chooses inputs and formats outputs.

## How a row is graded

1. **Fair price.** If the sharp book (`SHARP_BOOK`, default `pinnacle`) quotes
   the market, its de-vigged line alone is the fair estimate. Otherwise the
   desk falls back to a consensus of the other books, **excluding the book you
   are betting at** so it is not grading its own number. Rows on the weaker
   benchmark are tagged `consensus` in the UI and listed under
   `consensus_only_markets` in the API.
2. **De-vig.** `DEVIG_METHOD` — `power` (default), `multiplicative`, or
   `additive`. This is the one real judgement call in the whole desk; see below.
3. **EV and stake.** EV per dollar at your price against the fair probability,
   then `BANKROLL × kelly × KELLY_MULTIPLIER` (default 0.25) as the stake.

`MIN_EV=0` shows only +EV rows. Set `MIN_EV=-1` to see everything, which is the
honest view — on the demo feed exactly 1 of 26 prices is +EV.

## The de-vig choice matters more than anything else here

Same longshot, same feed, three methods:

| method | fair prob | fair price | EV/$ at +1600 |
|---|---|---|---|
| power | 5.71% | +1652 | **−2.99%** |
| multiplicative | 6.58% | +1419 | **+11.92%** |
| additive | 6.04% | +1557 | +2.61% |

Multiplicative de-vig calls that bet a 12% edge. Power calls it a 3% loser.
Proportional de-vig preserves the raw ratio between outcomes, which on a
lopsided market carries the book's favourite-longshot bias straight into your
"fair" number. Power is the default for that reason.

Additive has a harder failure: it subtracts the same absolute amount from every
outcome, so on a deep longshot it can return a probability at or below zero.
`devig_additive` documents it and `test_additive_can_go_negative_on_a_longshot`
pins it. The desk never defaults to it.

## Overround is not hold

`overround()` is `sum(implied) − 1`. `hold()` is `1 − 1/sum(implied)`. On a
−110/−110 market that is 4.762% and 4.545%. Hold is the number books quote;
using overround in its place overstates the margin on every market.

## Parlays

Priced by multiplying decimal odds, graded leg by leg against each market's
fair estimate. Independence is assumed, and the report says so. Three
qualifications come back in `notes`:

- **Missing fair estimate** on any leg → EV is `null`, not guessed.
- **Same game** → legs are correlated, so the independent product is not the
  true probability. EV is indicative only.
- **Duplicate outcome** → staking one outcome at two books does not compound.
  Its joint probability is its own probability, so the fair price and EV shown
  are wrong rather than merely optimistic.

`total_hold` compounds correctly: 4.545% per leg becomes 8.884% over two and
31.076% over eight, matching `1 − (1 − 0.04545)ⁿ` exactly.

## Feeds

| `FEED_SOURCE` | status |
|---|---|
| `demo` | tested. Deterministic fixtures, no network, no key. `DEMO_MODE=1` forces it. |
| `file` | tested. A JSON snapshot in the normalised shape — see the docstring in `desk/feed.py`. |
| `oddsapi` | **unverified.** Written to the documented the-odds-api v4 shape but never run against a live key from this build. Treat it as a starting point. |

`validate_markets` rejects a malformed snapshot rather than showing wrong
numbers: ragged book rows, markets with under two outcomes, no books, and
prices in the impossible −100…+100 gap.

## API

```
GET  /api/health
GET  /api/markets
GET  /api/edges?stake=100&method=power&min_ev=-1
POST /api/parlay   {"stake": 20, "legs": [{"market_id", "outcome_index", "book"}]}
```

## Deliberate limits

- **No auth, loopback only.** Do not expose it as-is.
- **No live-odds polling.** The feed is read once at startup; restart to refresh.
- **No bet placement, no bankroll history.** It tells you the number; you place
  the bet and track it.
- **No correlation model.** Same-game parlays are flagged, never priced.
