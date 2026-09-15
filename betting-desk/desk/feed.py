"""
Where prices come from.

Three sources, selected by FEED_SOURCE (DEMO_MODE=1 forces "demo"):

  demo     deterministic fixtures below - no network, no key, same numbers
           every run, so the desk is reviewable and the tests are stable
  file     a JSON snapshot in the normalised shape (see MARKET_SHAPE)
  oddsapi  the-odds-api.com v4

Normalised market shape - every source must produce this:

    {
      "id":       "nfl-kc-buf-ml",        unique within a snapshot
      "sport":    "NFL",
      "game_id":  "kc-buf",               shared by markets in one game
      "commence": "2026-09-20T17:00:00Z",
      "label":    "Chiefs @ Bills - Moneyline",
      "outcomes": ["Chiefs", "Bills"],    order matters
      "books":    {"pinnacle": [-150, 135], ...}   same order as outcomes
    }
"""
from __future__ import annotations

import json
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any

from .config import ROOT, Settings

MARKET_SHAPE = ("id", "sport", "game_id", "commence", "label", "outcomes", "books")


# ---------- fixtures ----------
#
# Hand-built so the desk has something honest to show:
#   - kc-buf      soft books a touch off the sharp line -> small real edges
#   - lal-bos     every book agreed -> no edge, which is the normal case
#   - epl         three-way market, exercises non-binary de-vig
#   - longshot    lopsided prop where multiplicative and power disagree most
#   - nofair      sharp book absent -> consensus fallback path
#
DEMO_MARKETS: list[dict[str, Any]] = [
    {
        "id": "nfl-kc-buf-ml",
        "sport": "NFL",
        "game_id": "kc-buf",
        "commence": "2026-09-20T17:00:00Z",
        "label": "Chiefs @ Bills - Moneyline",
        "outcomes": ["Chiefs", "Bills"],
        "books": {
            "pinnacle":   [-150, 135],
            "fanduel":    [-140, 120],
            "draftkings": [-155, 130],
            "betmgm":     [-148, 126],
        },
    },
    {
        "id": "nfl-kc-buf-spread",
        "sport": "NFL",
        "game_id": "kc-buf",
        "commence": "2026-09-20T17:00:00Z",
        "label": "Chiefs @ Bills - Spread -2.5",
        "outcomes": ["Chiefs -2.5", "Bills +2.5"],
        "books": {
            "pinnacle":   [-108, -102],
            "fanduel":    [-115, -105],
            "draftkings": [-110, -110],
            "betmgm":     [-112, -108],
        },
    },
    {
        "id": "nba-lal-bos-ml",
        "sport": "NBA",
        "game_id": "lal-bos",
        "commence": "2026-09-21T00:30:00Z",
        "label": "Lakers @ Celtics - Moneyline",
        "outcomes": ["Lakers", "Celtics"],
        "books": {
            "pinnacle":   [225, -270],
            "fanduel":    [220, -265],
            "draftkings": [222, -268],
            "betmgm":     [218, -262],
        },
    },
    {
        "id": "epl-ars-mci-1x2",
        "sport": "EPL",
        "game_id": "ars-mci",
        "commence": "2026-09-20T11:30:00Z",
        "label": "Arsenal v Man City - 1X2",
        "outcomes": ["Arsenal", "Draw", "Man City"],
        "books": {
            "pinnacle":   [175, 240, 155],
            "fanduel":    [170, 235, 145],
            "draftkings": [180, 225, 150],
            "betmgm":     [165, 230, 160],
        },
    },
    {
        "id": "nfl-kc-buf-anytime-td",
        "sport": "NFL",
        "game_id": "kc-buf",
        "commence": "2026-09-20T17:00:00Z",
        "label": "Chiefs @ Bills - Longshot anytime TD",
        "outcomes": ["Scores a TD", "No TD"],
        "books": {
            "pinnacle":   [1400, -1750],
            "fanduel":    [1600, -2000],
            "draftkings": [1500, -1900],
        },
    },
    {
        "id": "mlb-nyy-lad-total",
        "sport": "MLB",
        "game_id": "nyy-lad",
        "commence": "2026-09-20T23:10:00Z",
        "label": "Yankees @ Dodgers - Total 8.5 (no sharp price)",
        "outcomes": ["Over 8.5", "Under 8.5"],
        "books": {
            "fanduel":    [-105, -115],
            "draftkings": [-110, -110],
            "betmgm":     [-102, -120],
        },
    },
]


class FeedError(RuntimeError):
    pass


def validate_markets(markets: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Fail loudly on a malformed snapshot rather than showing wrong numbers."""
    for i, m in enumerate(markets):
        missing = [k for k in MARKET_SHAPE if k not in m]
        if missing:
            raise FeedError(f"market {i} ({m.get('id', '?')}) missing keys: {missing}")
        n = len(m["outcomes"])
        if n < 2:
            raise FeedError(f"market {m['id']} needs at least 2 outcomes")
        if not m["books"]:
            raise FeedError(f"market {m['id']} has no books")
        for book, prices in m["books"].items():
            if len(prices) != n:
                raise FeedError(
                    f"market {m['id']}: book {book} quotes {len(prices)} prices "
                    f"for {n} outcomes"
                )
            for p in prices:
                if -100 < p < 100:
                    raise FeedError(
                        f"market {m['id']}: book {book} price {p} is not a valid "
                        "american price (nothing lies between -100 and +100)"
                    )
    return markets


def load_demo() -> list[dict[str, Any]]:
    return validate_markets([dict(m) for m in DEMO_MARKETS])


def load_file(path: str | Path) -> list[dict[str, Any]]:
    p = Path(path)
    if not p.is_absolute():
        p = ROOT / p
    if not p.exists():
        raise FeedError(f"FEED_FILE not found: {p}")
    try:
        data = json.loads(p.read_text())
    except json.JSONDecodeError as exc:
        raise FeedError(f"FEED_FILE is not valid JSON: {exc}") from exc
    if isinstance(data, dict) and "markets" in data:
        data = data["markets"]
    if not isinstance(data, list):
        raise FeedError("FEED_FILE must hold a list of markets, or {'markets': [...]}")
    return validate_markets(data)


# ---------- the-odds-api adapter ----------
#
# NOT EXERCISED IN THIS SESSION: it needs a paid/keyed host that is not
# reachable from the build container, so it is written to the documented v4
# response shape and typed only. Treat it as unverified until you have run it
# against a real key. `demo` and `file` are the tested paths.

def _oddsapi_get(url: str, timeout: float = 15.0) -> Any:
    try:
        with urllib.request.urlopen(url, timeout=timeout) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", "replace")[:400]
        raise FeedError(f"odds API returned {exc.code}: {body}") from exc
    except urllib.error.URLError as exc:
        raise FeedError(f"odds API unreachable: {exc.reason}") from exc


def load_oddsapi(settings: Settings, sport: str = "americanfootball_nfl",
                 market: str = "h2h") -> list[dict[str, Any]]:
    if not settings.odds_api_key:
        raise FeedError(
            "FEED_SOURCE=oddsapi needs ODDS_API_KEY. Set DEMO_MODE=1 to run on "
            "fixtures instead."
        )
    query = urllib.parse.urlencode({
        "apiKey": settings.odds_api_key,
        "regions": "us,eu",
        "markets": market,
        "oddsFormat": "american",
    })
    url = f"{settings.odds_api_base}/sports/{sport}/odds?{query}"
    events = _oddsapi_get(url)

    out: list[dict[str, Any]] = []
    for ev in events:
        home, away = ev.get("home_team"), ev.get("away_team")
        if not home or not away:
            continue
        outcomes = [away, home]
        books: dict[str, list[float]] = {}
        for bm in ev.get("bookmakers", []):
            for mk in bm.get("markets", []):
                if mk.get("key") != market:
                    continue
                prices = {o["name"]: o["price"] for o in mk.get("outcomes", [])}
                if all(name in prices for name in outcomes):
                    books[bm["key"]] = [float(prices[name]) for name in outcomes]
        if len(books) < 2:
            continue
        game_id = f"{away}-{home}".lower().replace(" ", "-")
        out.append({
            "id": f"{ev.get('id', game_id)}-{market}",
            "sport": ev.get("sport_title", sport),
            "game_id": game_id,
            "commence": ev.get("commence_time", ""),
            "label": f"{away} @ {home} - {market}",
            "outcomes": outcomes,
            "books": books,
        })
    if not out:
        raise FeedError("odds API returned no market with two or more books")
    return validate_markets(out)


def load_markets(settings: Settings) -> tuple[list[dict[str, Any]], str]:
    """Returns (markets, source_label)."""
    source = settings.effective_source
    if source == "demo":
        return load_demo(), "demo fixtures"
    if source == "file":
        if not settings.feed_file:
            raise FeedError("FEED_SOURCE=file needs FEED_FILE")
        return load_file(settings.feed_file), f"file:{settings.feed_file}"
    if source == "oddsapi":
        return load_oddsapi(settings), "the-odds-api v4"
    raise FeedError(f"unknown FEED_SOURCE: {source!r}")
