"""
ESPN's undocumented JSON endpoints.

Free, no API key, no auth. These are what espn.com calls to render its own
pages, which means two things: they are fast and complete, and ESPN owes you
nothing. They can change shape or disappear without notice, so every parser
here is defensive and returns partial data rather than raising.

Treat this as the schedule/score/context layer. It does not carry betting
odds - that comes from odds_api.py.
"""
from __future__ import annotations

import datetime as dt
from typing import Any

import httpx

BASE = "https://site.api.espn.com/apis/site/v2/sports"
CORE = "https://site.api.espn.com/apis/v2/sports"

# sport path, league path
LEAGUES: dict[str, tuple[str, str]] = {
    "nfl":   ("football", "nfl"),
    "ncaaf": ("football", "college-football"),
    "mlb":   ("baseball", "mlb"),
    "nba":   ("basketball", "nba"),
    "wnba":  ("basketball", "wnba"),
    "ncaab": ("basketball", "mens-college-basketball"),
    "nhl":   ("hockey", "nhl"),
    "epl":   ("soccer", "eng.1"),
    "mls":   ("soccer", "usa.1"),
}


class ESPNError(RuntimeError):
    pass


def _get(client: httpx.Client, url: str, params: dict | None = None) -> dict:
    r = client.get(url, params=params or {}, timeout=15.0)
    r.raise_for_status()
    return r.json()


def _num(v: Any) -> float | None:
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def scoreboard(league: str, date: dt.date | None = None) -> list[dict]:
    """
    Games for a league on a date (default today, ESPN's own idea of today).

    Returns a normalised list. Anything ESPN omits comes back as None rather
    than a filled-in guess.
    """
    if league not in LEAGUES:
        raise ESPNError(f"unknown league: {league}")
    sport, lg = LEAGUES[league]
    params: dict[str, Any] = {}
    if date:
        params["dates"] = date.strftime("%Y%m%d")
    if league == "ncaab":
        params.update({"groups": 50, "limit": 500})
    if league == "ncaaf":
        params.update({"groups": 80, "limit": 500})

    with httpx.Client(headers={"User-Agent": "betting-desk/1.0"}) as c:
        data = _get(c, f"{BASE}/{sport}/{lg}/scoreboard", params)

    out: list[dict] = []
    for ev in data.get("events", []):
        comp = (ev.get("competitions") or [{}])[0]
        cs = comp.get("competitors") or []
        home = next((x for x in cs if x.get("homeAway") == "home"), {})
        away = next((x for x in cs if x.get("homeAway") == "away"), {})
        status = ((ev.get("status") or {}).get("type") or {})
        venue = comp.get("venue") or {}

        out.append({
            "espn_id": ev.get("id"),
            "league": league,
            "name": ev.get("name"),
            "short_name": ev.get("shortName"),
            "start_utc": ev.get("date"),
            "state": status.get("state"),          # pre | in | post
            "status_detail": status.get("detail"),
            "completed": bool(status.get("completed")),
            "neutral_site": comp.get("neutralSite"),
            "venue": venue.get("fullName"),
            "indoor": venue.get("indoor"),
            "home": _team(home),
            "away": _team(away),
            "broadcast": _first_broadcast(comp),
        })
    return out


def _team(c: dict) -> dict:
    t = c.get("team") or {}
    rec = ""
    for r in c.get("records") or []:
        if r.get("type") in ("total", "overall") or r.get("name") == "overall":
            rec = r.get("summary", "")
            break
    return {
        "id": t.get("id"),
        "abbr": t.get("abbreviation"),
        "name": t.get("displayName"),
        "short": t.get("shortDisplayName"),
        "logo": t.get("logo"),
        "score": _num(c.get("score")),
        "record": rec or None,
    }


def _first_broadcast(comp: dict) -> str | None:
    for b in comp.get("broadcasts") or []:
        names = b.get("names") or []
        if names:
            return names[0]
    return None


def standings(league: str) -> list[dict]:
    """Team records. NHL needs the /apis/v2/ path; the others use /apis/site/v2/."""
    if league not in LEAGUES:
        raise ESPNError(f"unknown league: {league}")
    sport, lg = LEAGUES[league]
    base = CORE if league == "nhl" else BASE
    with httpx.Client(headers={"User-Agent": "betting-desk/1.0"}) as c:
        data = _get(c, f"{base}/{sport}/{lg}/standings")

    rows: list[dict] = []

    def walk(node: dict) -> None:
        for entry in (node.get("standings") or {}).get("entries", []) or []:
            team = entry.get("team") or {}
            stats = {s.get("name"): s.get("value") for s in entry.get("stats", [])}
            rows.append({
                "abbr": team.get("abbreviation"),
                "name": team.get("displayName"),
                "wins": stats.get("wins"),
                "losses": stats.get("losses"),
                "win_pct": stats.get("winPercent"),
                "point_diff": stats.get("pointDifferential"),
                "group": node.get("name"),
            })
        for child in node.get("children", []) or []:
            walk(child)

    walk(data)
    return rows


def injuries(league: str) -> list[dict]:
    """
    Injury report by team. Coverage varies by league - NFL and NBA are good,
    MLB is thinner. Empty list is a normal answer, not an error.
    """
    if league not in LEAGUES:
        raise ESPNError(f"unknown league: {league}")
    sport, lg = LEAGUES[league]
    out: list[dict] = []
    with httpx.Client(headers={"User-Agent": "betting-desk/1.0"}) as c:
        try:
            data = _get(c, f"{BASE}/{sport}/{lg}/injuries")
        except httpx.HTTPError:
            return out
        for team_block in data.get("injuries", []) or []:
            team = (team_block.get("team") or {}).get("abbreviation") or team_block.get("displayName")
            for inj in team_block.get("injuries", []) or []:
                ath = inj.get("athlete") or {}
                out.append({
                    "team": team,
                    "player": ath.get("displayName"),
                    "position": ((ath.get("position") or {}).get("abbreviation")),
                    "status": inj.get("status"),
                    "detail": (inj.get("type") or {}).get("description")
                              or inj.get("shortComment"),
                    "date": inj.get("date"),
                })
    return out


def summary(league: str, espn_id: str) -> dict:
    """Box score, leaders and win probability for one game."""
    if league not in LEAGUES:
        raise ESPNError(f"unknown league: {league}")
    sport, lg = LEAGUES[league]
    with httpx.Client(headers={"User-Agent": "betting-desk/1.0"}) as c:
        return _get(c, f"{BASE}/{sport}/{lg}/summary", {"event": espn_id})
