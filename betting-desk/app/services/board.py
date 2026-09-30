"""
Merge the free context layer (ESPN) with the paid odds layer, then run the
math over it.

The rule this whole file exists to enforce: a number is either sourced or
absent. If a book is missing a market, the cell is None. Nothing is filled
in from a neighbouring book, an average, or a previous refresh.
"""
from __future__ import annotations

import datetime as dt
import difflib
from typing import Any, Sequence

from ..math_engine import (
    devig, hold, overround, implied_to_american, ev_percent,
    expected_value, kelly_fraction, american_to_decimal,
    consensus_fair_probs, push_possible,
)

MIN_BOOKS_FOR_CONSENSUS = 3
GAME_MARKETS = ("h2h", "spreads", "totals")

# Two feeds listing the "same" game more than this far apart are not the
# same game. Guards the fuzzy name match on big college slates, where
# "Miami" can mean two different schools on the same Saturday.
MAX_START_GAP_HOURS = 12


# ---------- matching ESPN games to Odds API games ----------

def _norm(s: str | None) -> str:
    return (s or "").lower().replace(".", "").replace("'", "").strip()


def _parse_utc(s: str | None) -> dt.datetime | None:
    if not s:
        return None
    try:
        t = dt.datetime.fromisoformat(s.replace("Z", "+00:00"))
    except ValueError:
        return None
    return t if t.tzinfo else t.replace(tzinfo=dt.timezone.utc)


def _start_gap_ok(a: str | None, b: str | None) -> bool:
    """True unless both times parse and are too far apart to be one game."""
    ta, tb = _parse_utc(a), _parse_utc(b)
    if ta is None or tb is None:
        return True
    return abs((ta - tb).total_seconds()) <= MAX_START_GAP_HOURS * 3600


def match_games(espn_games: list[dict], odds_games: list[dict]) -> list[dict]:
    """
    Join on team names plus start time. The two feeds disagree on naming
    ("LA Angels" vs "Los Angeles Angels"), so this fuzzy-matches and records
    how confident it was. Unmatched rows survive - they just carry less.
    """
    merged: list[dict] = []
    pool = list(odds_games)

    for g in espn_games:
        home = _norm(g["home"]["name"])
        away = _norm(g["away"]["name"])
        best, best_score = None, 0.0
        for o in pool:
            if not _start_gap_ok(g.get("start_utc"), o.get("start_utc")):
                continue
            s = (difflib.SequenceMatcher(None, home, _norm(o.get("home"))).ratio()
                 + difflib.SequenceMatcher(None, away, _norm(o.get("away"))).ratio()) / 2
            if s > best_score:
                best, best_score = o, s
        if best and best_score >= 0.72:
            pool.remove(best)
            merged.append({**g, "odds": best, "match_confidence": round(best_score, 3)})
        else:
            merged.append({**g, "odds": None, "match_confidence": None})

    # odds-feed games ESPN did not list (rare, but do not silently drop them)
    for leftover in pool:
        merged.append({
            "espn_id": None, "league": None,
            "name": f"{leftover.get('away')} at {leftover.get('home')}",
            "short_name": None, "start_utc": leftover.get("start_utc"),
            "state": "pre", "status_detail": None, "completed": False,
            "venue": None,
            "home": {"abbr": None, "name": leftover.get("home"), "score": None},
            "away": {"abbr": None, "name": leftover.get("away"), "score": None},
            "odds": leftover, "match_confidence": 0.0,
            "note": "In the odds feed but not on the ESPN scoreboard.",
        })
    return merged


# ---------- market extraction ----------

def _two_way(entries: list[dict] | None, home: str, away: str) -> dict | None:
    """Pull a home/away two-way market into a fixed order."""
    if not entries or len(entries) < 2:
        return None
    by_name = {_norm(e["name"]): e for e in entries}
    if "draw" in by_name:
        # three-way market; de-vigging two of three outcomes is wrong,
        # so report nothing rather than a confident wrong number
        return None
    h = by_name.get(_norm(home))
    a = by_name.get(_norm(away))
    if not h or not a or h.get("price") is None or a.get("price") is None:
        return None
    return {
        "home": {"price": h["price"], "point": h.get("point")},
        "away": {"price": a["price"], "point": a.get("point")},
    }


def _over_under(entries: list[dict] | None) -> dict | None:
    if not entries or len(entries) < 2:
        return None
    by = {_norm(e["name"]): e for e in entries}
    o, u = by.get("over"), by.get("under")
    if not o or not u or o.get("price") is None or u.get("price") is None:
        return None
    return {
        "over": {"price": o["price"], "point": o.get("point")},
        "under": {"price": u["price"], "point": u.get("point")},
    }


def _pair(market: str, entries: list[dict] | None, home: str, away: str) -> list[dict] | None:
    """[side_a, side_b] as {price, point} in fixed order, or None."""
    if market == "totals":
        ou = _over_under(entries)
        return [ou["over"], ou["under"]] if ou else None
    tw = _two_way(entries, home, away)
    return [tw["home"], tw["away"]] if tw else None


def main_line(quotes: dict[str, list[dict]]) -> float | None:
    """
    The line most books are dealing, keyed on the first side's point.

    Books hang different numbers (-3 at one, -3.5 at another), and prices on
    different lines are different bets: averaging them into one "fair"
    probability is meaningless. So pick the modal line, break ties toward
    the most balanced market (the book's own idea of the main line), and
    only compare books on it.
    """
    counts: dict[float, int] = {}
    balance: dict[float, float] = {}
    for pair in quotes.values():
        pt = pair[0].get("point")
        if pt is None:
            continue
        counts[pt] = counts.get(pt, 0) + 1
        gap = abs(american_to_decimal(pair[0]["price"]) - american_to_decimal(pair[1]["price"]))
        balance[pt] = min(balance.get(pt, gap), gap)
    if not counts:
        return None
    return min(counts, key=lambda pt: (-counts[pt], balance[pt], pt))


def market_quotes(game: dict, market: str, home: str, away: str) -> dict[str, list[dict]]:
    """{book: [side_a, side_b]} for every book quoting both sides."""
    out: dict[str, list[dict]] = {}
    books = ((game.get("odds") or {}).get("books") or {})
    for bkey, blob in books.items():
        pair = _pair(market, blob.get(market), home, away)
        if pair:
            out[bkey] = pair
    return out


def collect_market(
    game: dict, market: str, home: str, away: str
) -> dict[str, list[float]]:
    """
    {book: [side_a_price, side_b_price]} for one market across every book
    that quotes it complete, on the main line. Books quoting only one side
    are dropped - a half market cannot be de-vigged. Books on a different
    line are dropped too; see main_line() and off_line_books().
    """
    quotes = market_quotes(game, market, home, away)
    line = main_line(quotes) if market != "h2h" else None
    return {
        b: [pair[0]["price"], pair[1]["price"]]
        for b, pair in quotes.items()
        if line is None or pair[0].get("point") == line
    }


def off_line_books(game: dict, market: str, home: str, away: str) -> dict[str, dict]:
    """Books dealing a different number from the main line, with what they deal."""
    if market == "h2h":
        return {}
    quotes = market_quotes(game, market, home, away)
    line = main_line(quotes)
    return {
        b: {"point": pair[0].get("point"), "prices": [pair[0]["price"], pair[1]["price"]]}
        for b, pair in quotes.items()
        if pair[0].get("point") != line
    }


# ---------- analysis ----------

def fair_estimate(
    book_prices: dict[str, list[float]],
    method: str = "power",
    sharp_book: str | None = None,
    exclude: str | None = None,
) -> tuple[list[float], str] | None:
    """
    (fair probabilities, source label) from every book except `exclude`.

    Priority: the sharp book if it quotes the market (and is not the book
    being graded), then a consensus of 3+ books, then a thin 2-book
    consensus, then a single book. None when no book is left to ask, or
    when the de-vig produced an impossible probability (additive de-vig can
    go negative on long shots) - no number beats a wrong one.

    Why exclude: grading a book against a consensus that contains it pulls
    the fair price toward that book's own number, which shrinks every edge
    it has (and every overlay it hides) by roughly 1/N. The book being
    graded must not mark its own homework.
    """
    pool = {b: p for b, p in book_prices.items() if b != exclude}
    if not pool:
        return None
    if sharp_book and sharp_book in pool:
        fair, source = devig(pool[sharp_book], method), f"sharp:{sharp_book}"
    elif len(pool) >= MIN_BOOKS_FOR_CONSENSUS:
        fair, source = consensus_fair_probs(pool, method), f"consensus:{len(pool)} books"
    elif len(pool) == 1:
        only = next(iter(pool))
        fair, source = devig(pool[only], method), f"single book:{only}"
    else:
        fair, source = consensus_fair_probs(pool, method), f"thin consensus:{len(pool)} books"
    if any(not 0.0 < f < 1.0 for f in fair):
        return None
    if exclude is not None:
        source += f" (excl. {exclude})"
    return fair, source


def grade_price(price: float, fair_prob: float, stake: float) -> dict:
    """EV, dollar EV and Kelly for one price against one fair probability."""
    return {
        "ev_per_dollar": round(ev_percent(price, fair_prob), 5),
        "ev_dollars": round(expected_value(stake, price, fair_prob), 2),
        "kelly": round(kelly_fraction(price, fair_prob), 5),
    }


_NO_FAIR = {"fair_prob": None, "fair_price": None, "fair_source": None,
            "ev_per_dollar": None, "ev_dollars": None, "kelly": None}


def analyse_market(
    book_prices: dict[str, list[float]],
    outcome_labels: Sequence[str],
    your_books: Sequence[str],
    stake: float,
    method: str = "power",
    sharp_book: str | None = None,
) -> dict | None:
    """
    Fair price, hold, best available price and EV for one market, with any
    number of outcomes (two-way lines, three-way results, N-way props).

    Two different fair prices come out of this, on purpose:

    - sides[].fair_prob is the market's fair price from every book (or the
      sharp). It answers "what is this outcome worth?" and is what the board
      displays.
    - each graded price (your_books[], best_ev_per_dollar) is scored against
      a fair price that leaves the graded book out; see fair_estimate().
      Each your_books entry carries the fair_prob/fair_source it was graded
      against. When no other book quotes the market, EV is null rather than
      the book grading itself.

    Returns None when nothing usable is quoted.
    """
    usable = {b: p for b, p in book_prices.items() if p and len(p) == len(outcome_labels)}
    if not usable:
        return None

    market = fair_estimate(usable, method, sharp_book)
    if market is None:
        return None
    fair, source = market

    # leave-one-out fairs, computed once per book that gets graded
    loo: dict[str, tuple[list[float], str] | None] = {}

    def fair_without(book: str) -> tuple[list[float], str] | None:
        if book not in loo:
            loo[book] = fair_estimate(usable, method, sharp_book, exclude=book)
        return loo[book]

    sides: list[dict] = []
    for i, label in enumerate(outcome_labels):
        best_book, best_price = max(
            ((b, p[i]) for b, p in usable.items()),
            key=lambda bp: american_to_decimal(bp[1]),
        )
        yours = []
        for yb in your_books:
            if yb not in usable:
                continue
            price = usable[yb][i]
            row = {"book": yb, "price": price}
            ind = fair_without(yb)
            if ind is None:
                row.update(_NO_FAIR)
            else:
                row.update({
                    "fair_prob": round(ind[0][i], 5),
                    "fair_price": implied_to_american(ind[0][i]),
                    "fair_source": ind[1],
                    **grade_price(price, ind[0][i], stake),
                })
            row["hold"] = round(hold(usable[yb]), 5)
            yours.append(row)
        best_ind = fair_without(best_book)
        sides.append({
            "label": label,
            "fair_prob": round(fair[i], 5),
            "fair_price": implied_to_american(fair[i]),
            "best_price": best_price,
            "best_book": best_book,
            "best_ev_per_dollar": (round(ev_percent(best_price, best_ind[0][i]), 5)
                                   if best_ind else None),
            "your_books": yours,
        })

    return {
        "fair_source": source,
        "book_count": len(usable),
        "market_hold": {b: round(hold(p), 5) for b, p in usable.items()},
        "sides": sides,
    }


def _play_fields(yb: dict) -> dict:
    """The graded-price fields a play row copies off a your_books entry."""
    return {
        "book": yb["book"],
        "price": yb["price"],
        "fair_price": yb["fair_price"],
        "fair_prob": yb["fair_prob"],
        "fair_source": yb["fair_source"],
        "ev_per_dollar": yb["ev_per_dollar"],
        "ev_dollars": yb["ev_dollars"],
        "kelly": yb["kelly"],
        "quarter_kelly": round(yb["kelly"] / 4, 5),
    }


def is_play(yb: dict, min_edge: float) -> bool:
    return yb.get("ev_per_dollar") is not None and yb["ev_per_dollar"] > min_edge


def build_board(
    espn_games: list[dict],
    odds_games: list[dict],
    your_books: Sequence[str],
    stake: float = 10.0,
    method: str = "power",
    sharp_book: str | None = None,
    min_edge: float = 0.0,
    markets: Sequence[str] = GAME_MARKETS,
) -> dict:
    """
    The full board: every game, every market you have prices for, with fair
    value and EV attached, plus a flat list of the spots that clear min_edge.

    markets limits which game markets are analysed. Every game still carries
    all three keys; one not asked for is None, same as one nobody quotes.
    """
    merged = match_games(espn_games, odds_games)
    games_out: list[dict] = []
    plays: list[dict] = []

    for g in merged:
        home_name = g["home"]["name"]
        away_name = g["away"]["name"]
        entry: dict[str, Any] = {
            "espn_id": g.get("espn_id"),
            "odds_id": (g.get("odds") or {}).get("odds_id"),
            "name": g.get("name"),
            "start_utc": g.get("start_utc"),
            "state": g.get("state"),
            "status_detail": g.get("status_detail"),
            "venue": g.get("venue"),
            "home": g["home"],
            "away": g["away"],
            "match_confidence": g.get("match_confidence"),
            "has_odds": g.get("odds") is not None,
            "markets": {m: None for m in GAME_MARKETS},
        }

        if g.get("odds"):
            specs = [
                ("h2h", ["home", "away"], [home_name, away_name]),
                ("spreads", ["home", "away"], [home_name, away_name]),
                ("totals", ["over", "under"], ["Over", "Under"]),
            ]
            for market, _, labels in specs:
                if market not in markets:
                    continue
                prices = collect_market(g, market, home_name, away_name)
                if not prices:
                    entry["markets"][market] = None
                    continue
                res = analyse_market(prices, labels, your_books, stake, method, sharp_book)
                if res:
                    pts = _main_points(g, market, home_name, away_name)
                    for side, pt in zip(res["sides"], pts):
                        side["point"] = pt
                        side["push_possible"] = push_possible(pt)
                    res["off_line_books"] = off_line_books(g, market, home_name, away_name)
                entry["markets"][market] = res

                if res:
                    for side in res["sides"]:
                        for yb in side["your_books"]:
                            if is_play(yb, min_edge):
                                plays.append({
                                    "game": g.get("name"),
                                    "start_utc": g.get("start_utc"),
                                    "market": market,
                                    "side": side["label"],
                                    "point": side.get("point"),
                                    "push_possible": side.get("push_possible", False),
                                    "odds_id": entry["odds_id"],
                                    **_play_fields(yb),
                                    "book_count": res["book_count"],
                                })
        games_out.append(entry)

    plays.sort(key=lambda p: p["ev_per_dollar"], reverse=True)
    return {
        "generated_at": dt.datetime.now(dt.timezone.utc).isoformat(),
        "your_books": list(your_books),
        "sharp_book": sharp_book,
        "devig_method": method,
        "stake": stake,
        "games": games_out,
        "plays": plays,
        "summary": {
            "games_total": len(games_out),
            "games_with_odds": sum(1 for g in games_out if g["has_odds"]),
            "plays_found": len(plays),
        },
    }


def _main_points(game: dict, market: str, home: str, away: str) -> list[float | None]:
    """Both sides' points on the main line, read off the first book dealing it."""
    quotes = market_quotes(game, market, home, away)
    line = main_line(quotes) if market != "h2h" else None
    for pair in quotes.values():
        if pair[0].get("point") == line:
            return [pair[0].get("point"), pair[1].get("point")]
    return [None, None]
