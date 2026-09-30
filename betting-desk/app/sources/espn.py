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
    """A failed ESPN call. `kind`: timeout, network, rate_limit, upstream, malformed, request."""

    def __init__(self, message: str, kind: str = "request"):
        super().__init__(message)
        self.kind = kind


TIMEOUT = 15.0


def _get(client: httpx.Client, url: str, params: dict | None = None) -> dict:
    try:
        r = client.get(url, params=params or {}, timeout=TIMEOUT)
    except httpx.TimeoutException as e:
        raise ESPNError(f"ESPN timed out after {TIMEOUT:.0f}s.", "timeout") from e
    except httpx.HTTPError as e:
        raise ESPNError(f"Could not reach ESPN: {e}", "network") from e
    if r.status_code == 429:
        raise ESPNError("ESPN is rate limiting requests (429).", "rate_limit")
    if r.status_code >= 400:
        raise ESPNError(f"ESPN returned HTTP {r.status_code}.", "upstream")
    try:
        data = r.json()
    except ValueError as e:
        raise ESPNError("ESPN returned a body that is not JSON.", "malformed") from e
    if not isinstance(data, dict):
        raise ESPNError("ESPN returned an unexpected shape.", "malformed")
    return data


def _dict(v: Any) -> dict:
    return v if isinstance(v, dict) else {}


def _list(v: Any) -> list:
    return v if isinstance(v, list) else []


def _num(v: Any) -> float | None:
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def scoreboard(league: str, date: dt.date | None = None,
               week: int | None = None) -> list[dict]:
    """
    Games for a league on a date (default today, ESPN's own idea of today).
    Football schedules by week, so NFL and NCAAF also take week= (regular
    season); ESPN ignores it for the other sports.

    Returns a normalised list. Anything ESPN omits comes back as None rather
    than a filled-in guess.
    """
    if league not in LEAGUES:
        raise ESPNError(f"unknown league: {league}")
    sport, lg = LEAGUES[league]
    params: dict[str, Any] = {}
    if date:
        params["dates"] = date.strftime("%Y%m%d")
    if week is not None and sport == "football":
        params.update({"week": week, "seasontype": 2})
    if league == "ncaab":
        params.update({"groups": 50, "limit": 500})
    if league == "ncaaf":
        params.update({"groups": 80, "limit": 500})

    with httpx.Client(headers={"User-Agent": "betting-desk/1.0"}) as c:
        data = _get(c, f"{BASE}/{sport}/{lg}/scoreboard", params)

    out: list[dict] = []
    for ev in _list(data.get("events")):
        if not isinstance(ev, dict):
            continue
        comp = _dict((_list(ev.get("competitions")) or [{}])[0])
        cs = [x for x in _list(comp.get("competitors")) if isinstance(x, dict)]
        home = next((x for x in cs if x.get("homeAway") == "home"), {})
        away = next((x for x in cs if x.get("homeAway") == "away"), {})
        status = _dict(_dict(ev.get("status")).get("type"))
        venue = _dict(comp.get("venue"))

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
    t = _dict(c.get("team"))
    rec = ""
    for r in _list(c.get("records")):
        if not isinstance(r, dict):
            continue
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
    for b in _list(comp.get("broadcasts")):
        names = _list(_dict(b).get("names"))
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
        for entry in _list(_dict(node.get("standings")).get("entries")):
            if not isinstance(entry, dict):
                continue
            team = _dict(entry.get("team"))
            stats = {s.get("name"): s.get("value")
                     for s in _list(entry.get("stats")) if isinstance(s, dict)}
            rows.append({
                "abbr": team.get("abbreviation"),
                "name": team.get("displayName"),
                "wins": stats.get("wins"),
                "losses": stats.get("losses"),
                "win_pct": stats.get("winPercent"),
                "point_diff": stats.get("pointDifferential"),
                "group": node.get("name"),
            })
        for child in _list(node.get("children")):
            if isinstance(child, dict):
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
        except ESPNError:
            return out
        for team_block in _list(data.get("injuries")):
            if not isinstance(team_block, dict):
                continue
            team = _dict(team_block.get("team")).get("abbreviation") or team_block.get("displayName")
            for inj in _list(team_block.get("injuries")):
                if not isinstance(inj, dict):
                    continue
                ath = _dict(inj.get("athlete"))
                out.append({
                    "team": team,
                    "player": ath.get("displayName"),
                    "position": _dict(ath.get("position")).get("abbreviation"),
                    "status": inj.get("status"),
                    "detail": _dict(inj.get("type")).get("description")
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
