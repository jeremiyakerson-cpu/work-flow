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
  main.py             FastAPI routes
  demo.py             frozen fixtures so you can run with no key
static/index.html     the UI
tests/                run with `python3 -m pytest tests/`
data/                 line-movement snapshots (SQLite, gitignored, created on first live refresh)
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
- `history`: `{recorded, enabled, error?}`, how many price changes this
  refresh wrote to the line-movement store (pass 2)
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

1. **Bet log with closing-line value.** Record every bet and the closing
   price, then compare. CLV is the only fast feedback on whether your process
   works — win rate takes hundreds of bets to say anything.
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
`tests/test_api_validation.py` covers bad input on the free routes.

CI: `.github/workflows/betting-desk-tests.yml` runs the suite on Python 3.11
and 3.12 for every PR that touches `betting-desk/`.
