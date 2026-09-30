"""
Line movement: every price the desk has seen, and how fair value moved.

Each live fetch (board or per-event markets) is written to a small SQLite
file as one row per (event, market, book, outcome). A row is written only
when the price or point changed since the last one stored for that key, so
refreshing inside the cache window, or on a quiet market, adds nothing.
The table is a change log, which is what line movement is.

Nothing here fetches anything. It records what the other routes already
paid for, and reads it back.
"""
from __future__ import annotations

import os
import sqlite3
import threading
from typing import Any, Iterable, Sequence

from .board import GAME_MARKETS, fair_estimate, main_line, _norm
from ..math_engine import implied_to_american

DEFAULT_DB = os.path.join(
    os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
    "data", "history.sqlite3",
)

_SCHEMA = """
CREATE TABLE IF NOT EXISTS snapshots (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    ts          TEXT NOT NULL,      -- when the desk saw it (UTC ISO)
    book_update TEXT,               -- the book's own last_update, if sent
    league      TEXT NOT NULL,
    event_id    TEXT NOT NULL,
    home        TEXT,
    away        TEXT,
    start_utc   TEXT,
    market      TEXT NOT NULL,
    book        TEXT NOT NULL,
    side        TEXT NOT NULL,
    description TEXT,               -- the player, on props
    point       REAL,
    price       REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_snap_event ON snapshots (event_id, market, ts);
"""


class HistoryStore:
    def __init__(self, path: str = DEFAULT_DB):
        self.path = path
        self._lock = threading.Lock()
        self._ready = False

    def _connect(self) -> sqlite3.Connection:
        con = sqlite3.connect(self.path)
        con.row_factory = sqlite3.Row
        if not self._ready:
            con.executescript(_SCHEMA)
            self._ready = True
        return con

    def record(self, league: str, rows: Iterable[dict], ts: str) -> int:
        """Store rows that differ from the latest stored value. Returns rows written."""
        rows = list(rows)
        if not rows:
            return 0
        os.makedirs(os.path.dirname(self.path) or ".", exist_ok=True)
        written = 0
        with self._lock, self._connect() as con:
            for r in rows:
                # A game market has one live line per book, so a move from
                # -3 to -3.5 and back to -3 must all be recorded: key on the
                # outcome and compare the point too. Per-event markets hang
                # many lines at once (alternates, prop ladders), so there the
                # point is part of the key.
                q = ("SELECT price, point FROM snapshots WHERE event_id=? AND market=? "
                     "AND book=? AND side=? AND description IS ?")
                args = [r["event_id"], r["market"], r["book"], r["side"], r.get("description")]
                if r["market"] not in GAME_MARKETS:
                    q += " AND point IS ?"
                    args.append(r.get("point"))
                last = con.execute(q + " ORDER BY id DESC LIMIT 1", args).fetchone()
                if (last is not None and last["price"] == r["price"]
                        and last["point"] == r.get("point")):
                    continue
                con.execute(
                    "INSERT INTO snapshots (ts, book_update, league, event_id, home, away, "
                    "start_utc, market, book, side, description, point, price) "
                    "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)",
                    (ts, r.get("book_update"), league, r["event_id"], r.get("home"),
                     r.get("away"), r.get("start_utc"), r["market"], r["book"], r["side"],
                     r.get("description"), r.get("point"), r["price"]),
                )
                written += 1
        return written

    def rows(self, event_id: str, market: str | None = None) -> list[dict]:
        if not os.path.exists(self.path):
            return []
        q = "SELECT * FROM snapshots WHERE event_id=?"
        args: list[Any] = [event_id]
        if market:
            q += " AND market=?"
            args.append(market)
        with self._lock, self._connect() as con:
            return [dict(r) for r in con.execute(q + " ORDER BY ts, id", args)]


# ---------- flattening what the fetchers return ----------

def rows_from_game_lines(odds_games: Sequence[dict]) -> list[dict]:
    """Snapshot rows from normalise_game_lines() output."""
    out: list[dict] = []
    for g in odds_games:
        for book, blob in (g.get("books") or {}).items():
            for market in GAME_MARKETS:
                for e in blob.get(market) or []:
                    if e.get("price") is None or not e.get("name"):
                        continue
                    out.append({
                        "event_id": g.get("odds_id"), "home": g.get("home"),
                        "away": g.get("away"), "start_utc": g.get("start_utc"),
                        "market": market, "book": book, "side": e["name"],
                        "description": e.get("description"), "point": e.get("point"),
                        "price": e["price"], "book_update": blob.get("last_update"),
                    })
    return out


def rows_from_event_blob(blob: dict) -> list[dict]:
    """Snapshot rows from one per-event odds response (props, alternates, periods)."""
    out: list[dict] = []
    for bm in blob.get("bookmakers") or []:
        for mk in bm.get("markets") or []:
            for o in mk.get("outcomes") or []:
                if o.get("price") is None or not o.get("name"):
                    continue
                out.append({
                    "event_id": blob.get("id"), "home": blob.get("home_team"),
                    "away": blob.get("away_team"), "start_utc": blob.get("commence_time"),
                    "market": mk.get("key"), "book": bm.get("key"), "side": o["name"],
                    "description": o.get("description"), "point": o.get("point"),
                    "price": o["price"],
                    "book_update": mk.get("last_update") or bm.get("last_update"),
                })
    return out


# ---------- reading it back ----------

def _side_order(market: str, home: str | None, away: str | None) -> list[str]:
    if market == "totals":
        return ["Over", "Under"]
    return [home or "", away or ""]


def build_history(
    rows: Sequence[dict],
    market: str,
    book: str | None = None,
    player: str | None = None,
    method: str = "power",
    sharp_book: str | None = None,
) -> dict:
    """
    Per-book price series for one market, and - for game markets - the fair
    price over time.

    series: one entry per (book, side, player, point), oldest first. A new
    point is a new series: -3 and -3.5 are different bets.

    fair: at every timestamp something moved, rebuild each book's quote as
    of then (its latest price on each side), take the main line the way the
    board does, and de-vig it the same way. Books that have not quoted yet
    are simply absent; nothing is carried back in time.
    """
    rows = [r for r in rows if r["market"] == market]
    if player:
        rows = [r for r in rows if (r.get("description") or "").lower() == player.lower()]
    shown = [r for r in rows if not book or r["book"] == book]

    series: dict[tuple, dict] = {}
    for r in shown:
        k = (r["book"], r["side"], r.get("description"), r.get("point"))
        s = series.setdefault(k, {
            "book": r["book"], "side": r["side"], "player": r.get("description"),
            "point": r.get("point"), "points": [],
        })
        s["points"].append({"t": r["ts"], "price": r["price"]})
    for s in series.values():
        pts = s["points"]
        s["open"], s["current"] = pts[0]["price"], pts[-1]["price"]
        s["moves"] = len(pts) - 1

    meta = rows[0] if rows else {}
    out: dict[str, Any] = {
        "event": {"home": meta.get("home"), "away": meta.get("away"),
                  "start_utc": meta.get("start_utc")},
        "market": market,
        "book": book,
        "snapshots": len(shown),
        "series": sorted(series.values(), key=lambda s: (s["book"], s["side"],
                                                          s["player"] or "",
                                                          s["point"] if s["point"] is not None else 0)),
        "fair": [],
    }
    if market in GAME_MARKETS and rows:
        out["fair"] = _fair_series(rows, market, meta.get("home"), meta.get("away"),
                                   method, sharp_book)
    elif rows:
        out["fair_note"] = "Fair-price history is computed for h2h, spreads and totals only."
    return out


def _fair_series(rows, market, home, away, method, sharp_book) -> list[dict]:
    order = _side_order(market, home, away)
    idx = {_norm(n): i for i, n in enumerate(order)}
    latest: dict[tuple[str, int], dict] = {}     # (book, side index) -> row
    points: list[dict] = []
    for ts in sorted({r["ts"] for r in rows}):
        for r in rows:
            if r["ts"] != ts:
                continue
            i = idx.get(_norm(r["side"]))
            if i is not None:
                latest[(r["book"], i)] = r
        quotes: dict[str, list[dict]] = {}
        for b in {b for b, _ in latest}:
            a, z = latest.get((b, 0)), latest.get((b, 1))
            if a and z:
                quotes[b] = [{"price": a["price"], "point": a["point"]},
                             {"price": z["price"], "point": z["point"]}]
        line = main_line(quotes) if market != "h2h" else None
        prices = {b: [q[0]["price"], q[1]["price"]] for b, q in quotes.items()
                  if line is None or q[0]["point"] == line}
        fair = fair_estimate(prices, method, sharp_book) if prices else None
        if not fair:
            continue
        points.append({
            "t": ts,
            "line": line,
            "book_count": len(prices),
            "fair_source": fair[1],
            "sides": [{"label": order[i], "fair_prob": round(p, 5),
                       "fair_price": implied_to_american(p)}
                      for i, p in enumerate(fair[0])],
        })
    return points
