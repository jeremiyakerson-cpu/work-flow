"""
Alerts: a play crossing your edge threshold, or a market moving sharply.

Evaluated on every board the desk builds, whether you pressed Refresh or the
scheduler did, so an alert can fire while the page is closed and be waiting
in the history when you come back.

Two rules, both about *changes*, so a refresh that finds the same thing
twice says nothing the second time:

- **edge**: a play (one of your books, one side, one line) whose EV per
  dollar is at or above `min_edge` and was not on the previous board for
  that league. A play that drops below and comes back alerts again.
- **move**: a market whose fair probability moved by `move_prob` or more
  since the last board, or whose main line (spread/total number) moved by
  `move_points` or more. One alert per market, named after the side that
  got more likely.

Boards with stale or failed odds are skipped: a feed outage that empties the
board would otherwise "reset" the edge state and re-alert everything when it
recovers.

Storage is a small JSON file (data/alerts.json, gitignored), capped at
MAX_ALERTS. Demo mode keeps everything in memory.
"""
from __future__ import annotations

import datetime as dt
import json
import os
import threading
from typing import Any, Iterable

DEFAULT_PATH = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "data", "alerts.json")
MAX_ALERTS = 500
GAME_MARKETS = ("h2h", "spreads", "totals")


def _env_float(name: str, default: float) -> float:
    try:
        return float(os.getenv(name, "") or default)
    except ValueError:
        return default


def default_config() -> dict:
    return {
        "enabled": os.getenv("ALERTS", "1") not in ("0", "false", "False"),
        "min_edge": _env_float("ALERT_MIN_EDGE", 0.03),      # EV per dollar, 0.03 = 3%
        "move_prob": _env_float("ALERT_MOVE_PROB", 0.03),    # fair probability, 0.03 = 3 points
        "move_points": _env_float("ALERT_MOVE_POINTS", 1.0), # spread / total number
    }


CONFIG_LIMITS = {"min_edge": (0.0, 1.0), "move_prob": (0.005, 1.0), "move_points": (0.5, 50.0)}


def _now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat()


def _game_key(g: dict) -> str:
    return str(g.get("odds_id") or g.get("espn_id") or g.get("name") or g.get("game"))


def play_key(league: str, p: dict) -> str:
    gid = p.get("odds_id") or p.get("game")
    return "|".join(str(x) for x in (league, gid, p.get("market"), p.get("player") or "",
                                     p.get("side"), p.get("point") if p.get("point") is not None else "",
                                     p.get("book")))


class AlertStore:
    def __init__(self, path: str | None = DEFAULT_PATH):
        self.path = path                     # None = memory only (demo mode, tests)
        self._lock = threading.Lock()
        self.config = default_config()
        self.alerts: list[dict] = []
        self.seq = 0
        self.read_upto = 0
        # league -> set of play keys above threshold on the last board
        self._above: dict[str, set[str]] = {}
        # (league, game, market) -> {"sides": {label: fair_prob}, "point": main line}
        self._fair: dict[tuple, dict] = {}
        self._load()

    # ---------- persistence ----------

    def _load(self) -> None:
        if not self.path or not os.path.exists(self.path):
            return
        try:
            with open(self.path) as f:
                blob = json.load(f)
        except (OSError, ValueError):
            return                               # a corrupt file is not worth a crash
        if not isinstance(blob, dict):
            return
        cfg = blob.get("config")
        if isinstance(cfg, dict):
            self._apply_config(cfg)
        self.alerts = [a for a in blob.get("alerts", []) if isinstance(a, dict)][-MAX_ALERTS:]
        self.seq = max([int(blob.get("seq") or 0)] + [int(a.get("id") or 0) for a in self.alerts])
        self.read_upto = int(blob.get("read_upto") or 0)

    def _save(self) -> None:
        if not self.path:
            return
        try:
            os.makedirs(os.path.dirname(self.path) or ".", exist_ok=True)
            tmp = self.path + ".tmp"
            with open(tmp, "w") as f:
                json.dump({"config": self.config, "seq": self.seq, "read_upto": self.read_upto,
                           "alerts": self.alerts}, f)
            os.replace(tmp, self.path)
        except OSError:
            pass                                 # read-only disk: alerts still work in memory

    # ---------- config ----------

    def _apply_config(self, cfg: dict) -> None:
        for k, v in cfg.items():
            if k == "enabled":
                self.config["enabled"] = bool(v)
            elif k in CONFIG_LIMITS and isinstance(v, (int, float)) and not isinstance(v, bool):
                lo, hi = CONFIG_LIMITS[k]
                self.config[k] = min(hi, max(lo, float(v)))

    def set_config(self, cfg: dict) -> dict:
        with self._lock:
            self._apply_config(cfg)
            self._save()
            return dict(self.config)

    # ---------- reading ----------

    def list(self, after: int = 0, limit: int = 100) -> list[dict]:
        with self._lock:
            rows = [a for a in self.alerts if a["id"] > after]
        return list(reversed(rows))[:limit]      # newest first

    def unread(self) -> int:
        with self._lock:
            return sum(1 for a in self.alerts if a["id"] > self.read_upto)

    def mark_read(self, upto: int | None = None) -> int:
        with self._lock:
            self.read_upto = max(self.read_upto, upto if upto is not None else self.seq)
            self._save()
            return self.read_upto

    def clear(self) -> None:
        with self._lock:
            self.alerts = []
            self.read_upto = self.seq
            self._save()

    # ---------- evaluation ----------

    def evaluate(self, league: str, board: dict, origin: str = "manual") -> list[dict]:
        """Compare this board with the last one for the league. Returns the new alerts."""
        if not self.config.get("enabled", True):
            return []
        if board.get("stale_odds") or any(e.get("source") == "odds" for e in board.get("errors") or []):
            return []
        games = [g for g in board.get("games") or [] if isinstance(g, dict) and g.get("has_odds")]
        if not games:
            return []
        with self._lock:
            new = self._edge_alerts(league, board, games) + self._move_alerts(league, games)
            for a in new:
                self.seq += 1
                a.update(id=self.seq, league=league, origin=origin, at=_now())
                self.alerts.append(a)
            if new:
                self.alerts = self.alerts[-MAX_ALERTS:]
                self._save()
            return new

    def _edge_alerts(self, league: str, board: dict, games: list[dict]) -> list[dict]:
        thr = self.config["min_edge"]
        finished = {g.get("name") for g in games if g.get("state") == "post"}
        now_above: dict[str, dict] = {}
        for p in board.get("plays") or []:
            ev = p.get("ev_per_dollar")
            if not isinstance(ev, (int, float)) or ev < thr or ev <= 0 or p.get("game") in finished:
                continue
            now_above[play_key(league, p)] = p
        before = self._above.get(league)
        self._above[league] = set(now_above)
        out = []
        for k, p in now_above.items():
            if before is not None and k in before:
                continue
            pt = p.get("point")
            side = f"{p.get('side')}{'' if pt is None else ' ' + _fmt_point(p.get('market'), pt)}"
            out.append({
                "kind": "edge",
                "key": k,
                "title": f"+{ev_pct(p['ev_per_dollar'])} {side} at {p.get('book')}",
                "detail": (f"{p.get('game')} · {_market_name(p.get('market'))} · "
                           f"{_am(p.get('price'))} vs fair {_am(p.get('fair_price'))} "
                           f"({p.get('fair_source')})"),
                "game": p.get("game"), "odds_id": p.get("odds_id"), "market": p.get("market"),
                "side": p.get("side"), "point": pt, "book": p.get("book"), "price": p.get("price"),
                "ev_per_dollar": p.get("ev_per_dollar"), "threshold": thr,
                "start_utc": p.get("start_utc"),
            })
        return out

    def _move_alerts(self, league: str, games: list[dict]) -> list[dict]:
        out = []
        for g in games:
            if g.get("state") == "post":
                continue
            for mk in GAME_MARKETS:
                m = (g.get("markets") or {}).get(mk)
                if not isinstance(m, dict) or not isinstance(m.get("sides"), list):
                    continue
                sides = {s.get("label"): s for s in m["sides"] if isinstance(s, dict)}
                probs = {lb: s.get("fair_prob") for lb, s in sides.items()
                         if isinstance(s.get("fair_prob"), (int, float))}
                first = m["sides"][0] if m["sides"] else {}
                point = first.get("point") if isinstance(first, dict) else None
                key = (league, _game_key(g), mk)
                prev = self._fair.get(key)
                self._fair[key] = {"sides": probs, "point": point}
                if not prev or not probs:
                    continue
                a = self._one_move(g, mk, prev, probs, point, sides)
                if a:
                    out.append(a)
        return out

    def _one_move(self, g, mk, prev, probs, point, sides) -> dict | None:
        base = {"kind": "move", "game": g.get("name"), "odds_id": g.get("odds_id"),
                "market": mk, "start_utc": g.get("start_utc")}
        pp = prev.get("point")
        if (mk != "h2h" and isinstance(point, (int, float)) and isinstance(pp, (int, float))
                and abs(point - pp) >= self.config["move_points"]):
            lb = next(iter(sides), None)
            return {**base, "side": lb, "point": point, "from_point": pp,
                    "key": f"{base['odds_id'] or g.get('name')}|{mk}|line",
                    "title": f"Line move: {lb} {_fmt_point(mk, pp)} → {_fmt_point(mk, point)}",
                    "detail": f"{g.get('name')} · {_market_name(mk)} main line moved "
                              f"{abs(point - pp):g} points since the last refresh"}
        if point != pp:
            return None          # a new number: probabilities aren't comparable
        deltas = {lb: p - prev["sides"][lb] for lb, p in probs.items()
                  if isinstance(prev["sides"].get(lb), (int, float))}
        if not deltas:
            return None
        lb, d = max(deltas.items(), key=lambda kv: kv[1])
        if d < self.config["move_prob"]:
            return None
        s = sides.get(lb) or {}
        return {**base, "side": lb, "point": point, "key": f"{base['odds_id'] or g.get('name')}|{mk}|fair",
                "from_prob": round(prev["sides"][lb], 5), "to_prob": round(probs[lb], 5),
                "fair_price": s.get("fair_price"),
                "title": f"Steam: {lb}{'' if point is None else ' ' + _fmt_point(mk, s.get('point', point))} "
                         f"{ev_pct(prev['sides'][lb])} → {ev_pct(probs[lb])}",
                "detail": f"{g.get('name')} · {_market_name(mk)} fair price now {_am(s.get('fair_price'))}, "
                          f"up {d * 100:.1f} points of probability since the last refresh"}


# ---------- formatting ----------

def ev_pct(x: float) -> str:
    return f"{x * 100:.1f}%"


def _am(x: Any) -> str:
    if not isinstance(x, (int, float)):
        return "—"
    return f"+{x:g}" if x > 0 else f"{x:g}"


def _fmt_point(market: str | None, pt: Any) -> str:
    if not isinstance(pt, (int, float)):
        return ""
    if market == "totals":
        return f"{pt:g}"
    return f"+{pt:g}" if pt > 0 else f"{pt:g}"


def _market_name(m: str | None) -> str:
    return {"h2h": "Moneyline", "spreads": "Spread", "totals": "Total"}.get(
        m or "", (m or "").replace("_", " ").title())


def summarise(rows: Iterable[dict]) -> dict:
    rows = list(rows)
    return {"count": len(rows), "edge": sum(r["kind"] == "edge" for r in rows),
            "move": sum(r["kind"] == "move" for r in rows)}
