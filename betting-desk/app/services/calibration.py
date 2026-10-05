"""
Calibration and CLV report for logged plays.

The tracker lives in the browser, so this takes the plays as input and
keeps nothing. A play is the tracker's own record flattened:

    {league, market, book, price, stake, point,
     fair_prob,         # the tool's fair probability when you logged it
     ev_per_dollar,     # the edge it showed (computed from fair_prob if absent)
     result,            # won | lost | push | None (not graded yet)
     close_fair_prob,   # de-vigged fair probability at the close, if captured
     close_point}       # the close's line; a different number means no CLV

Two questions, answered separately:

- Calibration: when the tool said 55%, did those sides win about 55%?
  Reliability buckets, Brier score and log-loss, over graded plays only.
  Pushes are dropped (no outcome to score).
- CLV: did the prices you took beat the de-vigged close? Grouped by
  sport, market and the edge the tool showed at entry. CLV needs far fewer
  plays than win-rate to read, which is why thresholds.py leans on it.
"""
from __future__ import annotations

import math
from typing import Iterable, Sequence

from ..math_engine import (
    american_to_decimal, brier_score, log_loss, wilson_interval, clv_vs_fair, ev_percent,
)

# Below this many graded plays the report still shows numbers, flagged.
MIN_GRADED = 30
# A reliability bucket with fewer plays than this is flagged "thin".
MIN_BUCKET = 10

EDGE_BUCKETS: list[tuple[str, float, float]] = [
    ("<0%", -math.inf, 0.0),
    ("0-1%", 0.0, 0.01),
    ("1-2%", 0.01, 0.02),
    ("2-3%", 0.02, 0.03),
    ("3-5%", 0.03, 0.05),
    ("5%+", 0.05, math.inf),
]


def edge_bucket(ev: float | None) -> str | None:
    if ev is None:
        return None
    for label, lo, hi in EDGE_BUCKETS:
        if lo <= ev < hi:
            return label
    return None


def _f(v) -> float | None:
    try:
        x = float(v)
    except (TypeError, ValueError):
        return None
    return x if math.isfinite(x) else None


def normalise_play(p: dict) -> dict | None:
    """Fill derived fields (entry EV, CLV, outcome). None if the play has no usable price."""
    price = _f(p.get("price"))
    if price is None or -100 < price < 100:
        return None
    fair = _f(p.get("fair_prob"))
    if fair is not None and not 0.0 < fair < 1.0:
        fair = None
    ev = _f(p.get("ev_per_dollar"))
    if ev is None and fair is not None:
        ev = ev_percent(price, fair)
    close = _f(p.get("close_fair_prob"))
    if close is not None and not 0.0 < close < 1.0:
        close = None
    moved = (p.get("close_point") is not None
             and _f(p.get("close_point")) != _f(p.get("point")))
    clv = clv_vs_fair(price, close) if close is not None and not moved else None
    result = (p.get("result") or "").lower() or None
    if result not in ("won", "lost", "push"):
        result = None
    stake = _f(p.get("stake"))
    return {
        "league": (p.get("league") or "unknown").lower(),
        "market": p.get("market") or "unknown",
        "book": p.get("book"),
        "price": price,
        "stake": stake if stake and stake > 0 else 1.0,
        "fair_prob": fair,
        "ev": ev,
        "close_fair_prob": close,
        "clv": clv,
        "line_moved": moved,
        "result": result,
    }


def _profit(p: dict) -> float | None:
    if p["result"] == "won":
        return p["stake"] * (american_to_decimal(p["price"]) - 1.0)
    if p["result"] == "lost":
        return -p["stake"]
    if p["result"] == "push":
        return 0.0
    return None


def _mean(xs: Sequence[float]) -> float | None:
    return sum(xs) / len(xs) if xs else None


def _sd(xs: Sequence[float]) -> float | None:
    if len(xs) < 2:
        return None
    m = sum(xs) / len(xs)
    return math.sqrt(sum((x - m) ** 2 for x in xs) / (len(xs) - 1))


def _r(x: float | None, nd: int = 5) -> float | None:
    return None if x is None else round(x, nd)


def reliability(probs: Sequence[float], outcomes: Sequence[int], bins: int = 10) -> list[dict]:
    """
    Equal-width buckets over [0, 1]. Each non-empty bucket: n, mean
    predicted, observed rate with a 95% Wilson interval, and the gap
    (observed - predicted). A well-calibrated tool has gaps inside the
    intervals.
    """
    bins = max(1, int(bins))
    rows: list[dict] = []
    for k in range(bins):
        lo, hi = k / bins, (k + 1) / bins
        idx = [i for i, p in enumerate(probs) if lo <= p < hi or (k == bins - 1 and p == 1.0)]
        if not idx:
            continue
        n = len(idx)
        pred = sum(probs[i] for i in idx) / n
        wins = sum(outcomes[i] for i in idx)
        obs = wins / n
        ci = wilson_interval(wins, n)
        rows.append({
            "lo": round(lo, 4), "hi": round(hi, 4), "n": n,
            "mean_pred": round(pred, 5), "observed": round(obs, 5),
            "gap": round(obs - pred, 5),
            "ci95": [round(ci[0], 5), round(ci[1], 5)],
            "inside_ci": ci[0] <= pred <= ci[1],
            "status": "ok" if n >= MIN_BUCKET else "thin",
        })
    return rows


def expected_calibration_error(rows: Sequence[dict]) -> float | None:
    """Play-weighted mean |gap| over reliability buckets."""
    n = sum(r["n"] for r in rows)
    return sum(r["n"] * abs(r["gap"]) for r in rows) / n if n else None


def _score_block(probs: list[float], ys: list[int], bins: int) -> dict:
    if not probs:
        return {"n": 0, "brier": None, "log_loss": None, "base_rate": None,
                "brier_base_rate": None, "brier_skill": None, "mean_pred": None,
                "ece": None, "reliability": []}
    base = sum(ys) / len(ys)
    b = brier_score(probs, ys)
    b0 = brier_score([base] * len(ys), ys)
    rel = reliability(probs, ys, bins)
    return {
        "n": len(probs),
        "brier": _r(b, 6),
        "log_loss": _r(log_loss(probs, ys), 6),
        "base_rate": _r(base),
        # what you'd score always forecasting the observed win rate
        "brier_base_rate": _r(b0, 6),
        "brier_skill": _r(1.0 - b / b0, 5) if b0 > 0 else None,
        "mean_pred": _r(sum(probs) / len(probs)),
        "ece": _r(expected_calibration_error(rel)),
        "reliability": rel,
    }


def clv_group(plays: Sequence[dict]) -> dict:
    """n, CLV stats, record and ROI for any set of normalised plays."""
    clvs = [p["clv"] for p in plays if p["clv"] is not None]
    evs = [p["ev"] for p in plays if p["clv"] is not None and p["ev"] is not None]
    graded = [p for p in plays if p["result"] is not None]
    profit = sum(_profit(p) for p in graded)
    staked = sum(p["stake"] for p in graded if p["result"] != "push")
    sd = _sd(clvs)
    mean_clv, mean_ev = _mean(clvs), _mean(evs)
    return {
        "plays": len(plays),
        "with_clv": len(clvs),
        "mean_clv": _r(mean_clv),
        "clv_se": _r(sd / math.sqrt(len(clvs))) if sd is not None else None,
        "beat_close_pct": _r(sum(1 for c in clvs if c > 0) / len(clvs), 4) if clvs else None,
        "mean_entry_ev": _r(mean_ev),
        # share of the edge the tool showed that survived to the close
        "edge_realisation": (_r(mean_clv / mean_ev, 4)
                             if mean_clv is not None and mean_ev and mean_ev > 0 else None),
        "record": {r: sum(1 for p in graded if p["result"] == r) for r in ("won", "lost", "push")},
        "profit": round(profit, 2),
        "roi": _r(profit / staked, 5) if staked else None,
    }


def _grouped(plays: Sequence[dict], key) -> list[dict]:
    groups: dict[str, list[dict]] = {}
    for p in plays:
        k = key(p)
        if k is not None:
            groups.setdefault(k, []).append(p)
    return [{"key": k, **clv_group(v)} for k, v in groups.items()]


def calibration_report(raw_plays: Iterable[dict], bins: int = 10) -> dict:
    plays = [p for p in (normalise_play(r) for r in raw_plays) if p]
    graded = [p for p in plays if p["result"] in ("won", "lost")]

    entry = [(p["fair_prob"], 1 if p["result"] == "won" else 0)
             for p in graded if p["fair_prob"] is not None]
    close = [(p["close_fair_prob"], 1 if p["result"] == "won" else 0)
             for p in graded if p["close_fair_prob"] is not None]

    n = len(entry)
    if not plays:
        status, note = "no_plays", "No plays sent. POST the tracker's plays (see README)."
    elif n < MIN_GRADED:
        status, note = "not_enough_data", (
            f"{n} graded plays with a fair probability. Calibration reads need "
            f"{MIN_GRADED}+ and get trustworthy in the hundreds; numbers below are shown "
            "but are mostly noise.")
    else:
        status, note = "ok", None

    order = {b[0]: i for i, b in enumerate(EDGE_BUCKETS)}
    by_edge = sorted(_grouped(plays, lambda p: edge_bucket(p["ev"])), key=lambda g: order[g["key"]])
    out = {
        "status": status,
        "plays": len(plays),
        "graded": len(graded),
        "pushes": sum(1 for p in plays if p["result"] == "push"),
        "entry": _score_block([e[0] for e in entry], [e[1] for e in entry], bins),
        # the close is the benchmark: if the close is better calibrated than
        # your entry fair price, the entry model has room to improve
        "close": _score_block([c[0] for c in close], [c[1] for c in close], bins),
        "clv": {
            "overall": clv_group(plays),
            "by_league": sorted(_grouped(plays, lambda p: p["league"]), key=lambda g: g["key"]),
            "by_market": sorted(_grouped(plays, lambda p: p["market"]), key=lambda g: g["key"]),
            "by_league_market": sorted(_grouped(plays, lambda p: f'{p["league"]}/{p["market"]}'),
                                       key=lambda g: g["key"]),
            "by_edge": by_edge,
        },
        "definitions": {
            "brier": "mean (fair_prob - won)^2 over graded plays; lower is better",
            "brier_skill": "1 - brier / brier of always forecasting the observed win rate",
            "ece": "play-weighted mean |observed - predicted| across reliability buckets",
            "clv": "close_fair_prob x decimal(price) - 1; null when the line moved",
            "edge_realisation": "mean CLV / mean entry EV over plays with a close",
        },
    }
    if note:
        out["note"] = note
    return out
