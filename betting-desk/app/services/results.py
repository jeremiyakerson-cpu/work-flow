"""
Grading tracked plays against final scores.

The tracker lives in the browser; it posts the plays it wants graded and
gets back a result per play. Scores come from ESPN's scoreboard (free).

Only full-game moneylines, spreads and totals can be graded from a final
score. Anything else (props, period lines, alternates) comes back
"ungradeable" and the UI asks you to grade it by hand, because a guess
from the final score would be wrong often enough to poison the record.
"""
from __future__ import annotations

import datetime as dt
import difflib
import hashlib
from typing import Callable
from zoneinfo import ZoneInfo

GRADEABLE = ("h2h", "spreads", "totals")
_ET = ZoneInfo("America/New_York")   # ESPN's scoreboard dates are US Eastern days

# Demo fixtures never finish, so demo mode makes up a final for each game.
# Deterministic per game id so a demo record is stable across reloads.
_DEMO_RANGES = {"mlb": (0, 9), "nfl": (3, 38), "ncaaf": (0, 52), "nba": (88, 128),
                "wnba": (65, 100), "ncaab": (55, 95), "nhl": (0, 6)}


def _norm(s: str | None) -> str:
    return (s or "").lower().replace(".", "").replace("'", "").strip()


def _num(v) -> float | None:
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def espn_dates(start_utc: str | None) -> list[dt.date]:
    """The ESPN scoreboard day(s) a game can sit on: its Eastern date, then its UTC date."""
    if not start_utc:
        return []
    try:
        t = dt.datetime.fromisoformat(start_utc.replace("Z", "+00:00"))
    except ValueError:
        return []
    if t.tzinfo is None:
        t = t.replace(tzinfo=dt.timezone.utc)
    et, utc = t.astimezone(_ET).date(), t.astimezone(dt.timezone.utc).date()
    return [et] if et == utc else [et, utc]


def which_team(label: str | None, home: str | None, away: str | None) -> str | None:
    """'home' / 'away' for a side label, by closest name. None if it's neither."""
    lb = _norm(label)
    if not lb:
        return None
    scores = {side: difflib.SequenceMatcher(None, lb, _norm(name)).ratio()
              for side, name in (("home", home), ("away", away)) if name}
    if not scores:
        return None
    best = max(scores, key=scores.get)
    return best if scores[best] >= 0.6 else None


def grade(play: dict, game: dict | None) -> dict:
    """
    One play against one game. Returns {status, result, detail, scores...}.

    status: pending (not started), live, final, unknown (game not found)
    result: won | lost | push | ungradeable | None (not final yet)
    """
    if not game:
        return {"status": "unknown", "result": None,
                "detail": "Game not found on ESPN's scoreboard for that date."}
    hs, as_ = _num((game.get("home") or {}).get("score")), _num((game.get("away") or {}).get("score"))
    base = {"state": game.get("state"), "home": (game.get("home") or {}).get("name"),
            "away": (game.get("away") or {}).get("name"), "home_score": hs, "away_score": as_,
            "status_detail": game.get("status_detail")}
    state = game.get("state")
    if state != "post":
        return {**base, "status": "live" if state == "in" else "pending", "result": None,
                "detail": "Game in progress." if state == "in" else "Game hasn't started."}
    if hs is None or as_ is None:
        return {**base, "status": "final", "result": None, "detail": "Final, but ESPN sent no score."}

    market = play.get("market")
    out = {**base, "status": "final"}
    if market not in GRADEABLE:
        return {**out, "result": "ungradeable",
                "detail": "Only moneyline, spread and total grade from a final score. Grade this by hand."}

    point = _num(play.get("point"))
    side = play.get("side")
    if market == "totals":
        pick = _norm(side)
        if pick not in ("over", "under") or point is None:
            return {**out, "result": "ungradeable", "detail": "Total without an Over/Under and a line."}
        diff = (hs + as_) - point
        if pick == "under":
            diff = -diff
    else:
        team = which_team(side, base["home"], base["away"])
        if team is None:
            return {**out, "result": "ungradeable",
                    "detail": f"Couldn't tell which team '{side}' is. Grade this by hand."}
        diff = (hs - as_) if team == "home" else (as_ - hs)
        if market == "spreads":
            if point is None:
                return {**out, "result": "ungradeable", "detail": "Spread without a line."}
            diff += point
    result = "won" if diff > 0 else "lost" if diff < 0 else "push"
    return {**out, "result": result, "detail": f"Final {as_:g}-{hs:g} ({base['away']} at {base['home']})."}


def demo_game(play: dict) -> dict:
    """A made-up final for a demo play. Labelled as such by the caller."""
    lo, hi = _DEMO_RANGES.get(play.get("league") or "", (0, 40))
    seed = hashlib.sha256(str(play.get("espn_id") or play.get("game") or "").encode()).digest()
    span = hi - lo + 1
    home, away = lo + seed[0] % span, lo + seed[1] % span
    if home == away:            # no ties: most of these leagues don't end in one
        home += 1
    return {"state": "post", "status_detail": "Final (demo, synthetic)",
            "home": {"name": play.get("home"), "score": home},
            "away": {"name": play.get("away"), "score": away}}


def grade_plays(
    plays: list[dict],
    scoreboard: Callable[[str, dt.date], list[dict]],
    demo: bool = False,
) -> dict:
    """
    Grade a batch. scoreboard(league, date) -> games; one call per
    (league, day), cached for the batch. A failing day becomes an entry in
    errors and its plays come back status "unknown" - never an exception.
    """
    results: dict[str, dict] = {}
    errors: list[dict] = []
    boards: dict[tuple[str, dt.date], list[dict] | None] = {}

    def games_on(league: str, day: dt.date) -> list[dict]:
        k = (league, day)
        if k not in boards:
            try:
                boards[k] = scoreboard(league, day)
            except Exception as e:
                boards[k] = None
                errors.append({"source": "scores", "kind": getattr(e, "kind", "unexpected"),
                               "message": f"{league.upper()} {day.isoformat()}: {e}"})
        return boards[k] or []

    for p in plays:
        pid = str(p.get("id") or "")
        if not pid:
            continue
        if demo:
            if not (p.get("home") and p.get("away")):
                results[pid] = {"status": "unknown", "result": None,
                                "detail": "Demo play has no team names to grade against."}
                continue
            results[pid] = {**grade(p, demo_game(p)), "synthetic": True}
            continue
        league = p.get("league") or ""
        game, failed = None, False
        for day in espn_dates(p.get("start_utc")):
            gs = games_on(league, day)
            failed = failed or boards.get((league, day)) is None
            game = next((g for g in gs if p.get("espn_id") and str(g.get("espn_id")) == str(p["espn_id"])), None) \
                or next((g for g in gs if p.get("game") and _norm(g.get("name")) == _norm(p["game"])), None)
            if game:
                break
        results[pid] = grade(p, game) if game or not failed else {
            "status": "unknown", "result": None, "detail": "Scores unavailable right now; try again."}
    return {"results": results, "errors": errors}
