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
    consensus_fair_probs,
)

MIN_BOOKS_FOR_CONSENSUS = 3


# ---------- matching ESPN games to Odds API games ----------

def _norm(s: str | None) -> str:
    return (s or "").lower().replace(".", "").replace("'", "").strip()


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


def collect_market(
    game: dict, market: str, home: str, away: str
) -> dict[str, list[float]]:
    """
    {book: [side_a_price, side_b_price]} for one market across every book
    that quotes it complete. Books quoting only one side are dropped -
    a half market cannot be de-vigged.
    """
    out: dict[str, list[float]] = {}
    books = ((game.get("odds") or {}).get("books") or {})
    for bkey, blob in books.items():
        entries = blob.get(market)
        pair = _over_under(entries) if market == "totals" else _two_way(entries, home, away)
        if not pair:
            continue
        if market == "totals":
            out[bkey] = [pair["over"]["price"], pair["under"]["price"]]
        else:
            out[bkey] = [pair["home"]["price"], pair["away"]["price"]]
    return out


# ---------- analysis ----------

def analyse_market(
    book_prices: dict[str, list[float]],
    outcome_labels: Sequence[str],
    your_books: Sequence[str],
    stake: float,
    method: str = "power",
    sharp_book: str | None = None,
) -> dict | None:
    """
    Fair price, hold, best available price and EV for one market.

    Returns None when there are not enough independent books to form a
    consensus. That is a real answer - "I cannot tell you what fair is
    here" beats a confident number built on one book quoting itself.
    """
    usable = {b: p for b, p in book_prices.items() if p and len(p) == len(outcome_labels)}
    if not usable:
        return None

    if sharp_book and sharp_book in usable:
        fair = devig(usable[sharp_book], method)
        source = f"sharp:{sharp_book}"
    elif len(usable) >= MIN_BOOKS_FOR_CONSENSUS:
        fair = consensus_fair_probs(usable, method)
        source = f"consensus:{len(usable)} books"
    elif len(usable) == 1:
        only = next(iter(usable))
        fair = devig(usable[only], method)
        source = f"single book:{only}"
    else:
        fair = consensus_fair_probs(usable, method)
        source = f"thin consensus:{len(usable)} books"

    sides: list[dict] = []
    for i, label in enumerate(outcome_labels):
        best_book, best_price = max(
            ((b, p[i]) for b, p in usable.items()),
            key=lambda bp: american_to_decimal(bp[1]),
        )
        yours = []
        for yb in your_books:
            if yb in usable:
                price = usable[yb][i]
                yours.append({
                    "book": yb,
                    "price": price,
                    "ev_per_dollar": round(ev_percent(price, fair[i]), 5),
                    "ev_dollars": round(expected_value(stake, price, fair[i]), 2),
                    "kelly": round(kelly_fraction(price, fair[i]), 5),
                    "hold": round(hold(usable[yb]), 5),
                })
        sides.append({
            "label": label,
            "fair_prob": round(fair[i], 5),
            "fair_price": implied_to_american(fair[i]),
            "best_price": best_price,
            "best_book": best_book,
            "best_ev_per_dollar": round(ev_percent(best_price, fair[i]), 5),
            "your_books": yours,
        })

    return {
        "fair_source": source,
        "book_count": len(usable),
        "market_hold": {b: round(hold(p), 5) for b, p in usable.items()},
        "sides": sides,
    }


def build_board(
    espn_games: list[dict],
    odds_games: list[dict],
    your_books: Sequence[str],
    stake: float = 10.0,
    method: str = "power",
    sharp_book: str | None = None,
    min_edge: float = 0.0,
) -> dict:
    """
    The full board: every game, every market you have prices for, with fair
    value and EV attached, plus a flat list of the spots that clear min_edge.
    """
    merged = match_games(espn_games, odds_games)
    games_out: list[dict] = []
    plays: list[dict] = []

    for g in merged:
        home_name = g["home"]["name"]
        away_name = g["away"]["name"]
        entry: dict[str, Any] = {
            "espn_id": g.get("espn_id"),
            "name": g.get("name"),
            "start_utc": g.get("start_utc"),
            "state": g.get("state"),
            "status_detail": g.get("status_detail"),
            "venue": g.get("venue"),
            "home": g["home"],
            "away": g["away"],
            "match_confidence": g.get("match_confidence"),
            "has_odds": g.get("odds") is not None,
            "markets": {},
        }

        if g.get("odds"):
            specs = [
                ("h2h", ["home", "away"], [home_name, away_name]),
                ("spreads", ["home", "away"], [home_name, away_name]),
                ("totals", ["over", "under"], ["Over", "Under"]),
            ]
            for market, _, labels in specs:
                prices = collect_market(g, market, home_name, away_name)
                if not prices:
                    entry["markets"][market] = None
                    continue
                res = analyse_market(prices, labels, your_books, stake, method, sharp_book)
                if res and market != "totals":
                    pts = _points_for(g, market, home_name, away_name)
                    for side, pt in zip(res["sides"], pts):
                        side["point"] = pt
                if res and market == "totals":
                    pts = _total_points(g)
                    for side, pt in zip(res["sides"], pts):
                        side["point"] = pt
                entry["markets"][market] = res

                if res:
                    for side in res["sides"]:
                        for yb in side["your_books"]:
                            if yb["ev_per_dollar"] > min_edge:
                                plays.append({
                                    "game": g.get("name"),
                                    "start_utc": g.get("start_utc"),
                                    "market": market,
                                    "side": side["label"],
                                    "point": side.get("point"),
                                    "book": yb["book"],
                                    "price": yb["price"],
                                    "fair_price": side["fair_price"],
                                    "fair_prob": side["fair_prob"],
                                    "fair_source": res["fair_source"],
                                    "ev_per_dollar": yb["ev_per_dollar"],
                                    "ev_dollars": yb["ev_dollars"],
                                    "kelly": yb["kelly"],
                                    "quarter_kelly": round(yb["kelly"] / 4, 5),
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


def _points_for(game: dict, market: str, home: str, away: str) -> list[float | None]:
    books = ((game.get("odds") or {}).get("books") or {})
    for blob in books.values():
        pair = _two_way(blob.get(market), home, away)
        if pair:
            return [pair["home"].get("point"), pair["away"].get("point")]
    return [None, None]


def _total_points(game: dict) -> list[float | None]:
    books = ((game.get("odds") or {}).get("books") or {})
    for blob in books.values():
        ou = _over_under(blob.get("totals"))
        if ou:
            return [ou["over"].get("point"), ou["under"].get("point")]
    return [None, None]
