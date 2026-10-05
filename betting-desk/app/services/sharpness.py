"""
Book sharpness: which books' prices best predict the closing line.

The closing line is the best public estimate of a game's true price, so a
book whose earlier prices sit close to the eventual close is carrying
information, and one that sits far from it is mostly noise (or shading).
This module measures that from the price-history store and turns it into
consensus weights for the board.

Per (league, market, book) it scores every recorded game that has already
started:

1. Rebuild each book's quote at every moment something moved, up to the
   start (the same reconstruction /api/history does for its fair series).
2. The close is the last pre-start state. Take the main line the way the
   board does. The target for book B is the de-vigged consensus of the
   OTHER books at the close (leave-one-out), so a book never gets credit
   for agreeing with its own closing number.
3. At every pre-start state where B quoted the close line, score B's
   de-vigged probabilities against that target with KL divergence
   (log-loss against the close, minus the close's own entropy). Average
   per game, then across games. Lower is sharper.

Weights: for independent noisy estimates of one probability the best
linear blend weights each by 1/variance, and for small errors KL is
proportional to squared error, so a book's raw weight is 1/mean KL
relative to the market's average. That raw weight is shrunk toward 1 by
sample size (n / (n + prior_events)) and clipped, so a few games can only
nudge the consensus. A market without enough history gets no weights at
all and the board keeps its plain average.

Nothing here fetches anything. It reads what the board already stored.
"""
from __future__ import annotations

import datetime as dt
import os
from dataclasses import dataclass, asdict
from typing import Iterable, Sequence

from ..math_engine import devig, consensus_fair_probs, kl_divergence
from .board import GAME_MARKETS, main_line, _norm, _parse_utc

# KL floor so a book that matched the close exactly cannot get infinite weight.
_KL_FLOOR = 1e-5


@dataclass(frozen=True)
class SharpnessConfig:
    """Every knob, with the env var that sets it. See README: Book sharpness."""
    min_events: int = 20          # SHARP_MIN_EVENTS: closed games in a market before weights apply
    min_book_events: int = 5      # SHARP_MIN_BOOK_EVENTS: games before one book's score counts
    min_books: int = 3            # books with a score before a market is weighted at all
    min_close_books: int = 2      # other books needed to form a leave-one-out close
    prior_events: float = 20.0    # SHARP_PRIOR_EVENTS: shrinkage toward equal weight
    weight_min: float = 0.25      # SHARP_WEIGHT_MIN
    weight_max: float = 4.0       # SHARP_WEIGHT_MAX

    @classmethod
    def from_env(cls) -> "SharpnessConfig":
        d = cls()

        def num(name: str, default, kind):
            try:
                v = kind(os.getenv(name, default))
            except (TypeError, ValueError):
                return default
            return v if v > 0 else default

        return cls(
            min_events=num("SHARP_MIN_EVENTS", d.min_events, int),
            min_book_events=num("SHARP_MIN_BOOK_EVENTS", d.min_book_events, int),
            min_books=d.min_books,
            min_close_books=d.min_close_books,
            prior_events=num("SHARP_PRIOR_EVENTS", d.prior_events, float),
            weight_min=num("SHARP_WEIGHT_MIN", d.weight_min, float),
            weight_max=num("SHARP_WEIGHT_MAX", d.weight_max, float),
        )


# ---------- rebuilding the market ----------

def _side_index(market: str, side: str, home: str | None, away: str | None) -> int | None:
    order = ["Over", "Under"] if market == "totals" else [home or "", away or ""]
    n = _norm(side)
    for i, name in enumerate(order):
        if n == _norm(name):
            return i
    return None


def market_states(rows: Sequence[dict], market: str, home: str | None,
                  away: str | None, until: dt.datetime | None) -> list[tuple[str, dict]]:
    """
    [(ts, {book: [side_a, side_b]})] oldest first: every book's latest
    two-sided quote at each moment something moved, up to `until`.
    """
    latest: dict[tuple[str, int], dict] = {}
    by_ts: dict[str, list[dict]] = {}
    for r in rows:
        if r["market"] != market:
            continue
        t = _parse_utc(r["ts"])
        if until is not None and (t is None or t > until):
            continue
        by_ts.setdefault(r["ts"], []).append(r)
    states: list[tuple[str, dict]] = []
    for ts in sorted(by_ts, key=lambda s: _parse_utc(s) or dt.datetime.min.replace(tzinfo=dt.timezone.utc)):
        for r in by_ts[ts]:
            i = _side_index(market, r["side"], home, away)
            if i is not None:
                latest[(r["book"], i)] = r
        quotes = {}
        for b in {b for b, _ in latest}:
            a, z = latest.get((b, 0)), latest.get((b, 1))
            if a and z:
                quotes[b] = [{"price": a["price"], "point": a.get("point")},
                             {"price": z["price"], "point": z.get("point")}]
        if quotes:
            states.append((ts, quotes))
    return states


def _on_line(pair: list[dict], line: float | None) -> bool:
    return line is None or pair[0].get("point") == line


def score_event(rows: Sequence[dict], market: str, method: str = "power",
                now: dt.datetime | None = None,
                config: SharpnessConfig = SharpnessConfig()) -> dict[str, dict] | None:
    """
    {book: {kl, abs_err, bias, observations}} for one game's market, or None
    when the game hasn't started, has no start time, or has no usable close.

    kl / abs_err / bias are means over the book's pre-start states on the
    closing line. abs_err and bias are on the first side's probability
    (home, or Over); bias > 0 means the book had that side too likely.
    """
    if not rows:
        return None
    meta = rows[0]
    start = _parse_utc(meta.get("start_utc"))
    now = now or dt.datetime.now(dt.timezone.utc)
    if start is None or start > now:
        return None
    states = market_states(rows, market, meta.get("home"), meta.get("away"), start)
    if not states:
        return None
    close = states[-1][1]
    line = main_line(close) if market != "h2h" else None
    on_close = {b: [q[0]["price"], q[1]["price"]] for b, q in close.items() if _on_line(q, line)}

    out: dict[str, dict] = {}
    for book in {b for _, q in states for b in q}:
        others = {b: p for b, p in on_close.items() if b != book}
        if len(others) < config.min_close_books:
            continue
        try:
            target = consensus_fair_probs(others, method)
        except ValueError:
            continue
        kls, errs, biases = [], [], []
        for _, quotes in states:
            q = quotes.get(book)
            if not q or not _on_line(q, line):
                continue
            est = devig([q[0]["price"], q[1]["price"]], method)
            if any(not 0.0 < p < 1.0 for p in est):
                continue
            kls.append(kl_divergence(target, est))
            errs.append(abs(est[0] - target[0]))
            biases.append(est[0] - target[0])
        if kls:
            out[book] = {"kl": sum(kls) / len(kls), "abs_err": sum(errs) / len(errs),
                         "bias": sum(biases) / len(biases), "observations": len(kls)}
    return out or None


# ---------- across games ----------

def _group_events(rows: Iterable[dict]) -> dict[str, list[dict]]:
    ev: dict[str, list[dict]] = {}
    for r in rows:
        if r.get("market") in GAME_MARKETS:
            ev.setdefault(r["event_id"], []).append(r)
    return ev


def book_weights(scores: dict[str, dict], config: SharpnessConfig) -> dict[str, dict]:
    """
    Raw and shrunk weights from per-book {events, mean_kl}. Books below
    min_book_events get weight 1.0 and raw None. Pure function; tested on
    known inputs.
    """
    scored = {b: s for b, s in scores.items() if s["events"] >= config.min_book_events}
    inv = {b: 1.0 / max(s["mean_kl"], _KL_FLOOR) for b, s in scored.items()}
    mean_inv = sum(inv.values()) / len(inv) if inv else None
    out: dict[str, dict] = {}
    for b, s in scores.items():
        if b not in inv or not mean_inv:
            out[b] = {"raw_weight": None, "weight": 1.0}
            continue
        raw = inv[b] / mean_inv
        trust = s["events"] / (s["events"] + config.prior_events)
        w = 1.0 + trust * (raw - 1.0)
        w = min(max(w, config.weight_min), config.weight_max)
        out[b] = {"raw_weight": round(raw, 4), "weight": round(w, 4)}
    return out


def sharpness_report(rows: Iterable[dict], league: str, method: str = "power",
                     now: dt.datetime | None = None,
                     config: SharpnessConfig | None = None,
                     markets: Sequence[str] = GAME_MARKETS) -> dict:
    """
    Per-market book table plus the weights the board would use. A market's
    `weights` is None (plain consensus) unless status is "ok".
    """
    config = config or SharpnessConfig()
    events = _group_events(r for r in rows if r.get("league") in (None, league))
    per_market: dict[str, dict] = {}
    for market in markets:
        agg: dict[str, dict] = {}
        n_events = 0
        for eid, ev_rows in events.items():
            mrows = [r for r in ev_rows if r["market"] == market]
            sc = score_event(mrows, market, method, now, config) if mrows else None
            if not sc:
                continue
            n_events += 1
            for b, s in sc.items():
                a = agg.setdefault(b, {"events": 0, "observations": 0,
                                       "kl": 0.0, "abs_err": 0.0, "bias": 0.0})
                a["events"] += 1
                a["observations"] += s["observations"]
                a["kl"] += s["kl"]
                a["abs_err"] += s["abs_err"]
                a["bias"] += s["bias"]
        scores = {b: {"events": a["events"], "observations": a["observations"],
                      "mean_kl": a["kl"] / a["events"],
                      "mean_abs_err": a["abs_err"] / a["events"],
                      "mean_bias": a["bias"] / a["events"]} for b, a in agg.items()}
        w = book_weights(scores, config)
        scored_books = sum(1 for b in scores if scores[b]["events"] >= config.min_book_events)
        if n_events < config.min_events:
            status, reason = "thin", (f"{n_events} closed games recorded; weighting starts at "
                                      f"{config.min_events}. Plain consensus is used.")
        elif scored_books < config.min_books:
            status, reason = "thin", (f"Only {scored_books} books have {config.min_book_events}+ "
                                      f"scored games; need {config.min_books}. Plain consensus is used.")
        else:
            status, reason = "ok", (f"{n_events} closed games. Weights are 1/KL relative to the "
                                    f"average book, shrunk toward 1 by n/(n+{config.prior_events:g}) "
                                    f"and clipped to [{config.weight_min:g}, {config.weight_max:g}].")
        books = [{
            "book": b,
            "events": s["events"],
            "observations": s["observations"],
            "mean_kl": round(s["mean_kl"], 6),
            "mean_abs_err": round(s["mean_abs_err"], 5),
            "mean_bias": round(s["mean_bias"], 5),
            **w[b],
        } for b, s in scores.items()]
        books.sort(key=lambda r: (r["events"] < config.min_book_events, r["mean_kl"]))
        for i, r in enumerate(books, 1):
            r["rank"] = i
        per_market[market] = {
            "status": status,
            "events": n_events,
            "reason": reason,
            "books": books,
            "weights": {r["book"]: r["weight"] for r in books} if status == "ok" else None,
        }
    return {
        "league": league,
        "method": method,
        "config": asdict(config),
        "markets": per_market,
        "metric": ("mean KL divergence (nats) of each book's de-vigged price from the "
                   "leave-one-out consensus close, over every pre-start state on the "
                   "closing line; lower is sharper"),
    }


def weights_for_board(report: dict) -> dict[str, dict[str, float]]:
    """{market: {book: weight}} for the markets the report says are ready."""
    return {m: r["weights"] for m, r in report.get("markets", {}).items() if r.get("weights")}
