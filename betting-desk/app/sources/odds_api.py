"""
The Odds API (v4) client.

Cost model matters here. A /odds call costs (markets x regions) credits, so
asking for h2h,spreads,totals from the us region is 3 credits per call, not 1.
Player props are per-event calls and get expensive fast: one event with two
prop markets is 2 credits, so a 15-game MLB slate is 30 credits for one
refresh. The free tier is 500 credits a month - that is roughly 16 full
prop refreshes and nothing else.

Every response carries x-requests-remaining and x-requests-used headers.
This client reads them and exposes them so the UI can show you what a
refresh actually cost before you burn the month in an afternoon.
"""
from __future__ import annotations

import time
from dataclasses import dataclass, field
from typing import Any

import httpx

BASE = "https://api.the-odds-api.com/v4"

# Odds API sport keys
SPORT_KEYS: dict[str, str] = {
    "nfl":   "americanfootball_nfl",
    "ncaaf": "americanfootball_ncaaf",
    "mlb":   "baseball_mlb",
    "nba":   "basketball_nba",
    "wnba":  "basketball_wnba",
    "ncaab": "basketball_ncaab",
    "nhl":   "icehockey_nhl",
    "epl":   "soccer_epl",
    "mls":   "soccer_usa_mls",
}

# Common player-prop market keys, per sport. Verify against the live
# /sports/{key}/events/{id}/markets endpoint - these change.
PROP_MARKETS: dict[str, list[str]] = {
    "mlb": ["batter_home_runs", "batter_hits", "batter_total_bases",
            "batter_rbis", "pitcher_strikeouts"],
    "nfl": ["player_pass_yds", "player_rush_yds", "player_reception_yds",
            "player_receptions", "player_anytime_td"],
    "nba": ["player_points", "player_rebounds", "player_assists",
            "player_threes"],
    "nhl": ["player_points", "player_shots_on_goal"],
}


@dataclass
class Usage:
    remaining: int | None = None
    used: int | None = None
    last_cost: int | None = None
    calls_this_session: int = 0

    def update(self, headers: httpx.Headers) -> None:
        def as_int(k: str) -> int | None:
            v = headers.get(k)
            try:
                return int(v) if v is not None else None
            except ValueError:
                return None
        self.remaining = as_int("x-requests-remaining")
        self.used = as_int("x-requests-used")
        self.last_cost = as_int("x-requests-last")
        self.calls_this_session += 1

    def to_dict(self) -> dict:
        return {
            "credits_remaining": self.remaining,
            "credits_used": self.used,
            "last_call_cost": self.last_cost,
            "calls_this_session": self.calls_this_session,
        }


@dataclass
class _CacheEntry:
    value: Any
    at: float


class OddsAPI:
    """
    Thin client with a TTL cache. The cache is the difference between a
    tool you can hammer and one that eats your month in an afternoon.
    """

    def __init__(self, api_key: str, cache_ttl: int = 120):
        if not api_key:
            raise ValueError("ODDS_API_KEY is not set")
        self.api_key = api_key
        self.cache_ttl = cache_ttl
        self.usage = Usage()
        self._cache: dict[str, _CacheEntry] = {}

    # ---------- plumbing ----------

    def _get(self, path: str, params: dict, cache: bool = True) -> Any:
        key = path + repr(sorted(params.items()))
        if cache:
            hit = self._cache.get(key)
            if hit and (time.time() - hit.at) < self.cache_ttl:
                return hit.value

        params = {**params, "apiKey": self.api_key}
        with httpx.Client(headers={"User-Agent": "betting-desk/1.0"}) as c:
            r = c.get(f"{BASE}{path}", params=params, timeout=20.0)
            self.usage.update(r.headers)
            if r.status_code == 401:
                raise RuntimeError("Odds API rejected the key (401).")
            if r.status_code == 422:
                raise RuntimeError(f"Odds API rejected the request (422): {r.text[:200]}")
            if r.status_code == 429:
                raise RuntimeError("Odds API rate limit or quota exhausted (429).")
            r.raise_for_status()
            data = r.json()

        if cache:
            self._cache[key] = _CacheEntry(data, time.time())
        return data

    def clear_cache(self) -> None:
        self._cache.clear()

    # ---------- endpoints ----------

    def sports(self) -> list[dict]:
        """In-season sports. Free - does not count against your quota."""
        return self._get("/sports", {})

    def odds(
        self,
        league: str,
        markets: tuple[str, ...] = ("h2h", "spreads", "totals"),
        regions: str = "us",
        bookmakers: tuple[str, ...] | None = None,
    ) -> list[dict]:
        """
        Game lines for a whole league in one call.

        Passing bookmakers instead of regions is cheaper when you only care
        about a few books: bookmakers-based calls cost markets x 1, not
        markets x regions.
        """
        key = SPORT_KEYS.get(league)
        if not key:
            raise ValueError(f"no Odds API sport key for {league}")
        params: dict[str, Any] = {
            "markets": ",".join(markets),
            "oddsFormat": "american",
            "dateFormat": "iso",
        }
        if bookmakers:
            params["bookmakers"] = ",".join(bookmakers)
        else:
            params["regions"] = regions
        return self._get(f"/sports/{key}/odds", params)

    def events(self, league: str) -> list[dict]:
        """Event list with ids. Free - needed to request props."""
        key = SPORT_KEYS[league]
        return self._get(f"/sports/{key}/events", {"dateFormat": "iso"})

    def event_odds(
        self,
        league: str,
        event_id: str,
        markets: tuple[str, ...],
        bookmakers: tuple[str, ...] | None = None,
        regions: str = "us",
    ) -> dict:
        """
        Player props for one event. Costs len(markets) credits per call when
        scoped to bookmakers. This is where the quota goes.
        """
        key = SPORT_KEYS[league]
        params: dict[str, Any] = {
            "markets": ",".join(markets),
            "oddsFormat": "american",
            "dateFormat": "iso",
        }
        if bookmakers:
            params["bookmakers"] = ",".join(bookmakers)
        else:
            params["regions"] = regions
        return self._get(f"/sports/{key}/events/{event_id}/odds", params)

    def scores(self, league: str, days_from: int = 1) -> list[dict]:
        key = SPORT_KEYS[league]
        return self._get(f"/sports/{key}/scores",
                         {"daysFrom": days_from, "dateFormat": "iso"})


# ---------- normalisation ----------

def normalise_game_lines(events: list[dict]) -> list[dict]:
    """
    Flatten the Odds API shape into one row per game with a
    {book: {market: {outcome: price}}} structure.

    Keeps every book. Filtering to your books happens later, because the
    books you cannot bet are exactly what makes the consensus useful.
    """
    out: list[dict] = []
    for ev in events:
        row = {
            "odds_id": ev.get("id"),
            "sport_key": ev.get("sport_key"),
            "start_utc": ev.get("commence_time"),
            "home": ev.get("home_team"),
            "away": ev.get("away_team"),
            "books": {},
        }
        for bm in ev.get("bookmakers", []) or []:
            bkey = bm.get("key")
            blob: dict[str, Any] = {"last_update": bm.get("last_update")}
            for mk in bm.get("markets", []) or []:
                mkey = mk.get("key")
                entries = []
                for o in mk.get("outcomes", []) or []:
                    entries.append({
                        "name": o.get("name"),
                        "price": o.get("price"),
                        "point": o.get("point"),
                        "description": o.get("description"),
                    })
                blob[mkey] = entries
            row["books"][bkey] = blob
        out.append(row)
    return out


def normalise_props(event_blob: dict) -> list[dict]:
    """
    One row per (market, player, side), with every book's price attached.

    Props are where line shopping pays best - soft books disagree far more
    on a rushing-yards number than on a moneyline.
    """
    rows: dict[tuple, dict] = {}
    for bm in event_blob.get("bookmakers", []) or []:
        book = bm.get("key")
        for mk in bm.get("markets", []) or []:
            market = mk.get("key")
            for o in mk.get("outcomes", []) or []:
                player = o.get("description") or o.get("name")
                side = o.get("name")
                point = o.get("point")
                k = (market, player, side, point)
                rows.setdefault(k, {
                    "market": market,
                    "player": player,
                    "side": side,
                    "point": point,
                    "prices": {},
                })
                rows[k]["prices"][book] = o.get("price")
    return list(rows.values())
