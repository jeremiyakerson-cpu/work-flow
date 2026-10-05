"""
Adaptive minimum edge: what edge the tool has to show, per sport and
market, before its plays actually beat the close.

The board's min_edge is one number for everything. But the tool's edges
are only as good as its fair price, and that varies: a liquid NFL spread
with four books is a better fair price than a three-book NHL total. The
history says how much of each market's shown edge survives to the close.

Method, per (league, market) with enough plays carrying a CLV:

1. Fit CLV = a + b * entry_edge by least squares. b is how much of a
   shown edge survives to the close; a is the offset (negative when the
   fair price overstates every edge by about the same amount).
2. The break-even edge is -a/b: below it the marginal play is expected to
   lose to the close, above it to beat it. That is the threshold that
   maximises total CLV, not just average CLV.
3. Round up to the 0.5% grid. Then check the plays the suggestion keeps:
   their mean CLV must be above zero at one-sided 90% confidence, or
   nothing is suggested.

Guards: too few plays -> "not_enough_data". A slope that is not clearly
positive means the shown edge doesn't predict CLV in this market ->
"edge_not_predictive". A break-even edge above the top of the grid ->
"no_threshold_beats_close".

CLV, not win-rate: CLV has a fraction of the variance of results, so it
reads in tens of plays where profit needs thousands. Results (ROI) are
shown alongside for reference only.

Nothing here is applied on its own. A suggestion is a suggestion; the
board only uses a threshold you adopt (PUT /api/thresholds/...) and only
when you ask for it (edge_profile=adopted).
"""
from __future__ import annotations

import datetime as dt
import json
import math
import os
import threading
from dataclasses import dataclass, asdict
from typing import Iterable, Sequence

from ..math_engine import linear_fit, break_even_x
from .calibration import normalise_play, _mean, _sd, _profit, _r

DEFAULT_GRID = tuple(round(0.005 * i, 4) for i in range(17))    # 0% .. 8%


@dataclass(frozen=True)
class ThresholdConfig:
    min_plays: int = 40          # plays with CLV in a market before anything is suggested
    min_subset: int = 20         # plays above a threshold before it can be the suggestion
    z: float = 1.2816            # one-sided 90%
    grid: tuple[float, ...] = DEFAULT_GRID


def threshold_curve(plays: Sequence[dict], config: ThresholdConfig) -> list[dict]:
    """One row per candidate threshold over normalised plays with clv and ev."""
    rows = []
    for t in config.grid:
        sub = [p for p in plays if p["ev"] >= t - 1e-12]
        clvs = [p["clv"] for p in sub]
        sd = _sd(clvs)
        mean = _mean(clvs)
        se = sd / math.sqrt(len(clvs)) if sd is not None else None
        lower = mean - config.z * se if mean is not None and se is not None else None
        graded = [p for p in sub if p["result"] is not None]
        staked = sum(p["stake"] for p in graded if p["result"] != "push")
        profit = sum(_profit(p) for p in graded)
        rows.append({
            "min_edge": t,
            "plays": len(sub),
            "mean_clv": _r(mean),
            "clv_se": _r(se),
            "clv_lower": _r(lower),
            "qualifies": bool(len(sub) >= config.min_subset and lower is not None and lower > 0),
            "graded": len(graded),
            "roi": _r(profit / staked, 5) if staked else None,
        })
    return rows


def _grid_up(x: float, grid: Sequence[float]) -> float | None:
    """Smallest grid value >= x (x <= 0 gives the first). None if x is past the grid."""
    for g in grid:
        if g >= x - 1e-12:
            return g
    return None


def suggest_for(plays: Sequence[dict], config: ThresholdConfig) -> dict:
    usable = [p for p in plays if p["clv"] is not None and p["ev"] is not None]
    base = {"plays": len(plays), "plays_with_clv": len(usable)}
    if len(usable) < config.min_plays:
        return {**base, "status": "not_enough_data", "suggested_min_edge": None,
                "needed": config.min_plays - len(usable),
                "reason": (f"{len(usable)} plays with a closing line; suggestions start at "
                           f"{config.min_plays}. Keep using your own min edge."),
                "fit": None, "curve": []}
    curve = threshold_curve(usable, config)
    fit = linear_fit([p["ev"] for p in usable], [p["clv"] for p in usable])
    fit_out = None
    if fit:
        fit_out = {"intercept": _r(fit["a"], 6), "slope": _r(fit["b"], 4),
                   "slope_se": _r(fit["se_b"], 4), "n": fit["n"]}
    if fit is None or fit["b"] - config.z * fit["se_b"] <= 0:
        return {**base, "status": "edge_not_predictive", "suggested_min_edge": None,
                "reason": ("The edge the tool showed doesn't clearly predict CLV here "
                           f"(slope not above zero at {_conf(config.z)} confidence), so a "
                           "bigger edge isn't evidence of a better bet. No threshold suggested."),
                "fit": fit_out, "curve": curve}
    be = break_even_x(fit)
    x0, se0 = be
    fit_out.update({"break_even_edge": _r(x0), "break_even_se": _r(se0)})
    t = _grid_up(max(x0, 0.0), config.grid)
    row = next((r for r in curve if r["min_edge"] == t), None) if t is not None else None
    if row is None or not row["qualifies"]:
        return {**base, "status": "no_threshold_beats_close", "suggested_min_edge": None,
                "reason": (f"Break-even edge is {x0 * 100:.2f}%, but the plays above it don't "
                           f"beat the close with {_conf(config.z)} confidence"
                           + (" (or there are too few of them)." if row else
                              " - it is past the top of the grid.")
                           + " The fair price in this market may not be good enough to bet off."),
                "fit": fit_out, "curve": curve}
    return {**base, "status": "suggested", "suggested_min_edge": t,
            "reason": (f"About {fit['b'] * 100:.0f}% of a shown edge survives to the close, "
                       f"offset {fit['a'] * 100:+.2f}%: the marginal play breaks even at "
                       f"{x0 * 100:.2f}% (\u00b1{se0 * 100:.2f}%). Plays at {t * 100:.1f}%+ beat "
                       f"the close by {row['mean_clv'] * 100:.2f}% on average over "
                       f"{row['plays']} plays."),
            "fit": fit_out, "basis": row, "curve": curve}


def _conf(z: float) -> str:
    return f"{round(100 * 0.5 * (1 + math.erf(z / math.sqrt(2))))}%"


def suggest_thresholds(raw_plays: Iterable[dict], config: ThresholdConfig | None = None) -> dict:
    config = config or ThresholdConfig()
    plays = [p for p in (normalise_play(r) for r in raw_plays) if p]
    groups: dict[tuple[str, str], list[dict]] = {}
    for p in plays:
        groups.setdefault((p["league"], p["market"]), []).append(p)
    out = [{"league": lg, "market": mk, **suggest_for(v, config)}
           for (lg, mk), v in sorted(groups.items())]
    return {
        "suggestions": out,
        "config": {**asdict(config), "grid": list(config.grid)},
        "applied": False,
        "note": ("Suggestions only. Nothing changes on the board until you adopt one "
                 "(PUT /api/thresholds/{league}/{market}) and ask the board for "
                 "edge_profile=adopted."),
    }


# ---------- adopted thresholds (explicit opt-in) ----------

class ThresholdStore:
    """
    {league: {market: {min_edge, adopted_at, note}}} in a small JSON file.
    path None keeps it in memory only (demo mode: nothing written to disk).
    """

    def __init__(self, path: str | None):
        self.path = path
        self._lock = threading.Lock()
        self._mem: dict = {}

    def load(self) -> dict:
        if self.path is None:
            return json.loads(json.dumps(self._mem))
        if not os.path.exists(self.path):
            return {}
        try:
            with open(self.path) as fh:
                data = json.load(fh)
        except (OSError, ValueError):
            return {}
        return data if isinstance(data, dict) else {}

    def _save(self, data: dict) -> None:
        if self.path is None:
            self._mem = data
            return
        os.makedirs(os.path.dirname(self.path) or ".", exist_ok=True)
        tmp = self.path + ".tmp"
        with open(tmp, "w") as fh:
            json.dump(data, fh, indent=2, sort_keys=True)
        os.replace(tmp, self.path)

    def adopt(self, league: str, market: str, min_edge: float, note: str | None = None) -> dict:
        with self._lock:
            data = self.load()
            entry = {"min_edge": min_edge,
                     "adopted_at": dt.datetime.now(dt.timezone.utc).isoformat(),
                     "note": note}
            data.setdefault(league, {})[market] = entry
            self._save(data)
            return entry

    def remove(self, league: str, market: str) -> bool:
        with self._lock:
            data = self.load()
            if market not in data.get(league, {}):
                return False
            del data[league][market]
            if not data[league]:
                del data[league]
            self._save(data)
            return True

    def for_league(self, league: str) -> dict[str, float]:
        return {m: float(e["min_edge"]) for m, e in self.load().get(league, {}).items()
                if isinstance(e, dict) and e.get("min_edge") is not None}
