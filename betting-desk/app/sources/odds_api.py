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

# Featured game markets. These are the only markets the bulk /odds endpoint
# serves; everything else (props, periods, alternates) is per-event.
GAME_MARKETS: tuple[str, ...] = ("h2h", "spreads", "totals")

# Player-prop market keys, per sport. Verify against the live
# /sports/{key}/events/{id}/markets endpoint - these change.
PROP_MARKETS: dict[str, list[str]] = {
    "mlb": ["batter_home_runs", "batter_hits", "batter_total_bases",
            "batter_rbis", "pitcher_strikeouts"],
    "nfl": ["player_pass_yds", "player_rush_yds", "player_reception_yds",
            "player_receptions", "player_anytime_td"],
    "ncaaf": ["player_pass_yds", "player_rush_yds", "player_reception_yds",
              "player_anytime_td"],
    "nba": ["player_points", "player_rebounds", "player_assists",
            "player_threes"],
    "nhl": ["player_points", "player_shots_on_goal",
            "player_goal_scorer_anytime"],
}

# What a props call asks for when you don't name markets. Deliberately
# short: every market is a credit, per game, per refresh.
DEFAULT_PROP_MARKETS: dict[str, list[str]] = {
    "mlb": ["batter_hits", "pitcher_strikeouts"],
    "nfl": ["player_pass_yds", "player_anytime_td"],
    "ncaaf": ["player_pass_yds", "player_rush_yds"],
    "nba": ["player_points", "player_rebounds"],
    "nhl": ["player_points", "player_shots_on_goal"],
}


def sport_key(league: str) -> str:
    key = SPORT_KEYS.get(league)
    if not key:
        raise ValueError(f"no Odds API sport key for {league}")
    return key


def estimate_cost(
    markets: tuple[str, ...] | list[str],
    bookmakers: tuple[str, ...] | list[str] | None = None,
    regions: str = "us",
) -> int:
    """
    Upper bound on credits for one call: markets x regions. Each group of
    up to ten bookmakers counts as one region. The API bills only markets
    that come back with data, so the real charge can be lower, never higher.
    """
    if bookmakers:
        region_units = -(-len(bookmakers) // 10)
    else:
        region_units = len([r for r in regions.split(",") if r.strip()]) or 1
    return len(markets) * region_units


class OddsAPIError(RuntimeError):
    """
    A failed Odds API call. `kind` is one of: timeout, network, auth,
    request, rate_limit, upstream, malformed, cache_miss.
    """

    def __init__(self, kind: str, message: str, status: int | None = None):
        super().__init__(message)
        self.kind = kind
        self.status = status


@dataclass
class Usage:
    remaining: int | None = None
    used: int | None = None
    last_cost: int | None = None
    calls_this_session: int = 0
    cache_hits: int = 0

    def update(self, headers: httpx.Headers) -> None:
        def as_int(k: str) -> int | None:
            v = headers.get(k)
            try:
                return int(v) if v is not None else None
            except ValueError:
                return None
        # a header missing from an error response keeps the last known value
        rem, used = as_int("x-requests-remaining"), as_int("x-requests-used")
        self.remaining = rem if rem is not None else self.remaining
        self.used = used if used is not None else self.used
        self.last_cost = as_int("x-requests-last")
        self.calls_this_session += 1

    def to_dict(self) -> dict:
        return {
            "credits_remaining": self.remaining,
            "credits_used": self.used,
            "last_call_cost": self.last_cost,
            "calls_this_session": self.calls_this_session,
            "cache_hits": self.cache_hits,
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

    def __init__(self, api_key: str, cache_ttl: int = 120, timeout: float = 20.0):
        if not api_key:
            raise ValueError("ODDS_API_KEY is not set")
        self.api_key = api_key
        self.cache_ttl = cache_ttl
        self.timeout = timeout
        self.last_stale = False
        self.last_fetched_at: float | None = None
        self.usage = Usage()
        self._cache: dict[str, _CacheEntry] = {}

    # ---------- plumbing ----------

    def _get(self, path: str, params: dict, cache: bool = True, cached_only: bool = False) -> Any:
        """
        GET with a TTL cache. Every failure mode (timeout, connection error,
        401/422/429, 5xx, a body that isn't JSON) raises OddsAPIError with a
        `kind` the caller can branch on. If the call fails but an older
        response for the same request is still in memory, that is returned
        instead, flagged via `self.last_stale`, so a flaky feed degrades to
        slightly old numbers rather than none.

        `cached_only=True` never touches the network (so never spends a
        credit): it returns the last response for this request however old,
        or raises OddsAPIError("cache_miss"). `self.last_fetched_at` is the
        epoch time the returned data was fetched.
        """
        key = path + repr(sorted(params.items()))
        self.last_stale = False
        hit = self._cache.get(key) if cache or cached_only else None
        if cached_only:
            if hit is None:
                raise OddsAPIError("cache_miss", "Nothing cached for this request yet; "
                                   "a normal refresh (or the scheduler) has to fetch it first.")
            self.usage.cache_hits += 1
            self.last_fetched_at = hit.at
            return hit.value
        if hit and (time.time() - hit.at) < self.cache_ttl:
            self.usage.cache_hits += 1
            self.last_fetched_at = hit.at
            return hit.value

        try:
            data = self._fetch(path, params)
        except OddsAPIError:
            if hit is not None:
                self.last_stale = True
                self.last_fetched_at = hit.at
                return hit.value
            raise

        self.last_fetched_at = time.time()
        if cache:
            self._cache[key] = _CacheEntry(data, self.last_fetched_at)
        return data

    def _fetch(self, path: str, params: dict) -> Any:
        params = {**params, "apiKey": self.api_key}
        try:
            with httpx.Client(headers={"User-Agent": "betting-desk/1.0"}) as c:
                r = c.get(f"{BASE}{path}", params=params, timeout=self.timeout)
        except httpx.TimeoutException as e:
            raise OddsAPIError("timeout", f"Odds API timed out after {self.timeout:.0f}s.") from e
        except httpx.HTTPError as e:
            raise OddsAPIError("network", f"Could not reach the Odds API: {e}") from e

        self.usage.update(r.headers)
        if r.status_code == 401:
            raise OddsAPIError("auth", "Odds API rejected the key (401).", 401)
        if r.status_code == 422:
            raise OddsAPIError("request", f"Odds API rejected the request (422): {r.text[:200]}", 422)
        if r.status_code == 429:
            retry = r.headers.get("retry-after")
            raise OddsAPIError(
                "rate_limit",
                "Odds API rate limit or monthly quota exhausted (429)."
                + (f" Retry after {retry}s." if retry else ""), 429)
        if r.status_code >= 400:
            raise OddsAPIError("upstream", f"Odds API returned HTTP {r.status_code}.", r.status_code)
        try:
            return r.json()
        except ValueError as e:
            raise OddsAPIError("malformed", "Odds API returned a body that is not JSON.") from e

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
        cached_only: bool = False,
    ) -> list[dict]:
        """
        Game lines for a whole league in one call.

        Passing bookmakers instead of regions is cheaper when you only care
        about a few books: bookmakers-based calls cost markets x 1, not
        markets x regions. `cached_only` serves the last response and never
        spends (see `_get`).
        """
        key = sport_key(league)
        params: dict[str, Any] = {
            "markets": ",".join(markets),
            "oddsFormat": "american",
            "dateFormat": "iso",
        }
        if bookmakers:
            params["bookmakers"] = ",".join(bookmakers)
        else:
            params["regions"] = regions
        return self._get(f"/sports/{key}/odds", params, cached_only=cached_only)

    def events(self, league: str) -> list[dict]:
        """Event list with ids. Free - needed to request props."""
        key = sport_key(league)
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
        key = sport_key(league)
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
        key = sport_key(league)
        return self._get(f"/sports/{key}/scores",
                         {"daysFrom": days_from, "dateFormat": "iso"})


# ---------- normalisation ----------

def _price(v: Any) -> float | None:
    """An American price, or None if the feed sent something unusable."""
    if isinstance(v, bool) or not isinstance(v, (int, float)):
        try:
            v = float(v)
        except (TypeError, ValueError):
            return None
    if v != v or v in (float("inf"), float("-inf")):   # NaN / inf
        return None
    # American odds live outside (-100, 100); 0 or +50 would blow up the math
    if -100 < v < 100:
        return None
    return v


def _list(v: Any) -> list:
    return v if isinstance(v, list) else []


def normalise_game_lines(events: Any) -> list[dict]:
    """
    Flatten the Odds API shape into one row per game with a
    {book: {market: {outcome: price}}} structure.

    Keeps every book. Filtering to your books happens later, because the
    books you cannot bet are exactly what makes the consensus useful.

    Malformed pieces (non-dict events, missing teams, prices that are not
    numbers) are dropped rather than raising - a partial board beats none.
    """
    out: list[dict] = []
    for ev in _list(events):
        if not isinstance(ev, dict) or not ev.get("home_team") or not ev.get("away_team"):
            continue
        row = {
            "odds_id": ev.get("id"),
            "sport_key": ev.get("sport_key"),
            "start_utc": ev.get("commence_time"),
            "home": ev.get("home_team"),
            "away": ev.get("away_team"),
            "books": {},
        }
        for bm in _list(ev.get("bookmakers")):
            if not isinstance(bm, dict) or not bm.get("key"):
                continue
            bkey = bm.get("key")
            blob: dict[str, Any] = {"last_update": bm.get("last_update")}
            for mk in _list(bm.get("markets")):
                if not isinstance(mk, dict) or not mk.get("key"):
                    continue
                mkey = mk.get("key")
                entries = []
                for o in _list(mk.get("outcomes")):
                    if not isinstance(o, dict) or not isinstance(o.get("name"), str):
                        continue
                    entries.append({
                        "name": o.get("name"),
                        "price": _price(o.get("price")),
                        "point": o.get("point") if isinstance(o.get("point"), (int, float)) else None,
                        "description": o.get("description"),
                    })
                blob[mkey] = entries
            row["books"][bkey] = blob
        out.append(row)
    return out


def normalise_props(event_blob: Any) -> list[dict]:
    """
    One row per (market, player, side), with every book's price attached.

    Props are where line shopping pays best - soft books disagree far more
    on a rushing-yards number than on a moneyline.
    """
    rows: dict[tuple, dict] = {}
    if not isinstance(event_blob, dict):
        return []
    for bm in _list(event_blob.get("bookmakers")):
        if not isinstance(bm, dict):
            continue
        book = bm.get("key")
        for mk in _list(bm.get("markets")):
            if not isinstance(mk, dict):
                continue
            market = mk.get("key")
            for o in _list(mk.get("outcomes")):
                if not isinstance(o, dict):
                    continue
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
                rows[k]["prices"][book] = _price(o.get("price"))
    return list(rows.values())
