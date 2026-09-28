"""
Per-event markets - player props, and anything else The Odds API only
serves one game at a time (period lines, alternates) - de-vigged and graded
the same way as the board.

Pairing is the whole job here. A prop is only de-viggable when a book quotes
both sides of the same line: Over and Under at the same point, Yes and No,
or two teams on mirrored spreads. Anything else is reported with a note and
no fair price, because a fair price built from one side is a guess.
"""
from __future__ import annotations

from typing import Any, Sequence

from ..math_engine import push_possible
from .board import analyse_market

_OVER_UNDER = ("Over", "Under")
_YES_NO = ("Yes", "No")


def _side_kind(name: str | None) -> tuple[str, ...] | None:
    n = (name or "").strip().lower()
    if n in ("over", "under"):
        return _OVER_UNDER
    if n in ("yes", "no"):
        return _YES_NO
    return None


def _canon(name: str | None) -> str:
    n = (name or "").strip()
    return n.capitalize() if _side_kind(n) else n


def collect_event_markets(event_blob: dict) -> list[dict]:
    """
    Group every outcome in an event-odds response into two-sided markets.

    Returns one row per (market, subject, line) with
    outcomes = {label: {book: price}}. subject is the player for props and
    None for team markets.
    """
    # (market, subject, side label, point) -> {book: price}
    prices: dict[tuple, dict[str, float]] = {}
    for bm in event_blob.get("bookmakers", []) or []:
        book = bm.get("key")
        for mk in bm.get("markets", []) or []:
            market = mk.get("key")
            for o in mk.get("outcomes", []) or []:
                if o.get("price") is None:
                    continue
                k = (market, o.get("description"), _canon(o.get("name")), o.get("point"))
                prices.setdefault(k, {})[book] = o.get("price")

    home = event_blob.get("home_team")
    rows: dict[tuple, dict] = {}
    for (market, subject, label, point), by_book in prices.items():
        kind = _side_kind(label)
        if kind:
            key = (market, subject, "ou" if kind is _OVER_UNDER else "yn", point)
            row = rows.setdefault(key, {
                "market": market, "player": subject, "point": point,
                "labels": list(kind), "outcomes": {},
            })
            row["outcomes"][label] = by_book
        else:
            # team-named sides pair on mirrored points: A -3.5 with B +3.5
            line = abs(point) if point is not None else None
            key = (market, subject, "team", line)
            row = rows.setdefault(key, {
                "market": market, "player": subject, "point": None,
                "labels": [], "outcomes": {}, "_points": {},
            })
            tag = label if point is None else f"{label} {point:+g}"
            row["outcomes"][tag] = by_book
            row["_points"][tag] = (label, point)

    out: list[dict] = []
    for row in rows.values():
        pts = row.pop("_points", None)
        if pts is None:
            out.append(row)
            continue
        out.extend(_split_team_row(row, pts, home))
    return out


def _split_team_row(row: dict, pts: dict[str, tuple], home: str | None) -> list[dict]:
    """
    A team row at |point| can hold two bets (A -3.5/B +3.5 and A +3.5/B -3.5
    when alternates are in play). Pair each side with its mirror; home first.
    """
    tags = sorted(pts, key=lambda t: (pts[t][0] != home, t))
    used: set[str] = set()
    out: list[dict] = []
    for t in tags:
        if t in used:
            continue
        name, pt = pts[t]
        mate = next((u for u in tags if u not in used and u != t
                     and pts[u][0] != name
                     and (pt is None and pts[u][1] is None
                          or pt is not None and pts[u][1] == -pt)), None)
        used.add(t)
        labels = [t] if mate is None else [t, mate]
        if mate:
            used.add(mate)
        out.append({
            "market": row["market"], "player": row["player"], "point": pt,
            "labels": labels,
            "outcomes": {lb: row["outcomes"][lb] for lb in labels},
        })
    return out


def analyse_event_markets(
    event_blob: dict,
    your_books: Sequence[str],
    stake: float = 10.0,
    method: str = "power",
    sharp_book: str | None = None,
    min_edge: float = 0.0,
) -> dict:
    """De-vig every paired market in one event and list the plays above min_edge."""
    rows = collect_event_markets(event_blob)
    props: list[dict] = []
    plays: list[dict] = []

    for r in rows:
        labels = r["labels"]
        present = [lb for lb in labels if lb in r["outcomes"]]
        both: dict[str, list[float]] = {}
        if len(present) == 2 and len(labels) == 2:
            books = set(r["outcomes"][labels[0]]) & set(r["outcomes"][labels[1]])
            both = {b: [r["outcomes"][labels[0]][b], r["outcomes"][labels[1]][b]]
                    for b in sorted(books)}

        analysis = None
        if both:
            analysis = analyse_market(both, labels, your_books, stake, method, sharp_book)
            for side in analysis["sides"]:
                side["point"] = r["point"] if _side_kind(side["label"]) else _team_point(side["label"])
                side["push_possible"] = push_possible(side["point"])

        item: dict[str, Any] = {
            "market": r["market"],
            "player": r["player"],
            "point": r["point"],
            "labels": labels,
            "outcomes": r["outcomes"],
            "analysis": analysis,
            "note": None if both else
                    "Only one side quoted - cannot strip vig on this market.",
        }
        # the original /api/props shape, kept for over/under props
        if labels == list(_OVER_UNDER):
            item["over"] = r["outcomes"].get("Over", {})
            item["under"] = r["outcomes"].get("Under", {})
        props.append(item)

        if analysis:
            for side in analysis["sides"]:
                for yb in side["your_books"]:
                    if yb["ev_per_dollar"] > min_edge:
                        plays.append({
                            "market": r["market"],
                            "player": r["player"],
                            "side": side["label"],
                            "point": side["point"],
                            "push_possible": side["push_possible"],
                            "book": yb["book"],
                            "price": yb["price"],
                            "fair_price": side["fair_price"],
                            "fair_prob": side["fair_prob"],
                            "fair_source": analysis["fair_source"],
                            "ev_per_dollar": yb["ev_per_dollar"],
                            "ev_dollars": yb["ev_dollars"],
                            "kelly": yb["kelly"],
                            "quarter_kelly": round(yb["kelly"] / 4, 5),
                            "book_count": analysis["book_count"],
                        })

    props.sort(key=lambda p: (p["market"], p["player"] or "", p["point"] if p["point"] is not None else 0))
    plays.sort(key=lambda p: p["ev_per_dollar"], reverse=True)
    return {
        "event": {
            "odds_id": event_blob.get("id"),
            "home": event_blob.get("home_team"),
            "away": event_blob.get("away_team"),
            "start_utc": event_blob.get("commence_time"),
        },
        "props": props,
        "plays": plays,
        "summary": {
            "markets_seen": len({p["market"] for p in props}),
            "lines": len(props),
            "lines_priced": sum(1 for p in props if p["analysis"]),
            "plays_found": len(plays),
        },
    }


def _team_point(tag: str) -> float | None:
    head, _, tail = tag.rpartition(" ")
    if not head:
        return None
    try:
        return float(tail)
    except ValueError:
        return None
