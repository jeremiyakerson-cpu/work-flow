"""
SYNTHETIC fixtures for the adaptive model (book sharpness, calibration,
suggested thresholds) so DEMO_MODE=1 shows every report with no key.

Nothing here is a record of a real market. Everything is generated from a
fixed seed so the demo, and the tests that pin it, are stable.

Sharpness history: for each demo league, 30 finished games with all three
game markets. Every book's de-vigged price at each snapshot is the true
close plus noise whose size is set per book in BOOK_NOISE, so the
sharpness ranking is known in advance (draftkings sharpest, caesars
softest) and the tests can check the model recovers it.

Tracked plays: a few hundred plays across leagues and markets. The tool's
entry fair price overstates every edge by a per-league amount
(EDGE_OVERSTATEMENT), so CLV is negative on small shown edges and positive
on large ones, which is what the threshold suggester has to find. Results
are drawn from the close, the best estimate of the truth.
"""
from __future__ import annotations

import datetime as dt
import random

from .math_engine import implied_to_american
from . import demo

LABEL = "DEMO FIXTURE (synthetic adaptive-model data)"

# sd of each book's no-vig probability around the close, before scaling by horizon
BOOK_NOISE = {"draftkings": 0.006, "fanduel": 0.014, "betmgm": 0.028, "caesars": 0.045}
_DEFAULT_NOISE = 0.03
# hours before start, and how much noisier a quote that far out is
_STEPS = [(48, 1.5), (20, 1.2), (6, 1.0), (1, 0.6)]
_VIG = 0.045
HISTORY_GAMES = 30


def _priced(p: float, rng: random.Random) -> tuple[int, int]:
    """No-vig p for side A -> two American prices carrying ~4.5% overround."""
    p = min(max(p, 0.04), 0.96)
    over = 1.0 + _VIG * (0.8 + 0.4 * rng.random())
    return implied_to_american(min(p * over, 0.985)), implied_to_american(min((1 - p) * over, 0.985))


def _books(league: str) -> list[str]:
    d = demo.demo_league(league)
    seen: list[str] = []
    for ev in (d["odds"] if d else []):
        for bm in ev.get("bookmakers", []):
            if bm["key"] not in seen:
                seen.append(bm["key"])
    return seen or list(BOOK_NOISE)


def demo_sharpness_rows(league: str) -> list[dict]:
    """Snapshot rows (history-store shape) for HISTORY_GAMES finished demo games."""
    if demo.demo_league(league) is None:
        return []
    rng = random.Random(f"sharpness:{league}")
    books = _books(league)
    first = dt.datetime(2026, 8, 1, 23, 0, tzinfo=dt.timezone.utc)
    rows: list[dict] = []
    for g in range(HISTORY_GAMES):
        start = first + dt.timedelta(days=g)
        eid = f"hist_{league}_{g:02d}"
        home, away = f"Home {g:02d}", f"Away {g:02d}"
        for market, sides, point in (("h2h", (home, away), None),
                                     ("spreads", (home, away), -3.5),
                                     ("totals", ("Over", "Under"), 44.5)):
            lo, hi = (0.25, 0.75) if market == "h2h" else (0.4, 0.6)
            true = lo + (hi - lo) * rng.random()
            for hours, scale in _STEPS:
                ts = (start - dt.timedelta(hours=hours)).isoformat()
                for b in books:
                    sd = BOOK_NOISE.get(b, _DEFAULT_NOISE) * scale
                    pa, pb = _priced(true + rng.gauss(0.0, sd), rng)
                    for side, price, sign in ((sides[0], pa, 1), (sides[1], pb, -1)):
                        pt = None if point is None else (point * sign if market == "spreads" else point)
                        rows.append({
                            "ts": ts, "league": league, "event_id": eid, "home": home,
                            "away": away, "start_utc": start.isoformat(), "market": market,
                            "book": b, "side": side, "description": None, "point": pt,
                            "price": price,
                        })
    return rows


# per-league overstatement of the tool's entry fair probability, in probability points
EDGE_OVERSTATEMENT = {"nfl": 0.006, "nba": 0.011, "mlb": 0.009, "nhl": 0.016, "ncaaf": 0.02}
# how many plays per (league, market); some deliberately too few to suggest anything
PLAY_COUNTS = {
    ("nfl", "spreads"): 140, ("nfl", "totals"): 90, ("nfl", "h2h"): 60,
    ("nba", "spreads"): 110, ("nba", "totals"): 45,
    ("mlb", "h2h"): 120, ("mlb", "totals"): 35,
    ("nhl", "h2h"): 50, ("ncaaf", "spreads"): 14,
    ("nba", "player_points"): 12,
}


def demo_tracked_plays() -> list[dict]:
    """Synthetic tracker plays, flattened the way /api/calibration takes them."""
    rng = random.Random("tracked-plays")
    plays: list[dict] = []
    for (league, market), n in PLAY_COUNTS.items():
        delta = EDGE_OVERSTATEMENT.get(league, 0.01)
        for i in range(n):
            lo, hi = (0.3, 0.7) if market == "h2h" else (0.42, 0.58)
            close = lo + (hi - lo) * rng.random()
            fair = min(max(close + delta + rng.gauss(0.0, 0.012), 0.05), 0.95)
            ev = 0.08 * rng.random()
            dec = (1.0 + ev) / fair
            price = round((dec - 1) * 100) if dec >= 2 else round(-100 / (dec - 1))
            point = -3.0 if market == "spreads" and league == "nfl" and i % 9 == 0 else (
                -3.5 if market == "spreads" else 44.5 if market == "totals" else None)
            u = rng.random()
            push = point is not None and float(point).is_integer() and u < 0.08
            won = rng.random() < close
            graded = i < n - 5             # the last few are still pending
            plays.append({
                "id": f"demo-{league}-{market}-{i}",
                "league": league, "market": market,
                "book": ("fanduel", "draftkings")[i % 2],
                "price": price, "stake": 10.0, "point": point,
                "fair_prob": round(fair, 5),
                "ev_per_dollar": round(fair * (1 + (price / 100 if price > 0 else 100 / -price)) - 1, 5),
                "close_fair_prob": round(close, 5),
                "close_point": point,
                "result": (None if not graded else "push" if push else "won" if won else "lost"),
            })
    return plays
