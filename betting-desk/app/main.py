"""
Betting Desk API.

Run:  uvicorn app.main:app --reload
Then: http://127.0.0.1:8000  (UI)  /docs  (API explorer)
"""
from __future__ import annotations

import datetime as dt
import logging
import os
import re
from typing import Any

from fastapi import FastAPI, HTTPException, Query
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel, Field, field_validator

from .sources import espn
from .sources.odds_api import (
    OddsAPI, normalise_game_lines, SPORT_KEYS, GAME_MARKETS, PROP_MARKETS,
    DEFAULT_PROP_MARKETS, estimate_cost,
)
from .services.board import build_board
from .services.props import analyse_event_markets
from .services import history
from . import demo
from . import math_engine as M

APP_DIR = os.path.dirname(os.path.abspath(__file__))
STATIC_DIR = os.path.join(os.path.dirname(APP_DIR), "static")

app = FastAPI(title="Betting Desk", version="1.0")

# ---- config from env ----
YOUR_BOOKS = tuple(
    b.strip() for b in os.getenv("YOUR_BOOKS", "fanduel,draftkings").split(",") if b.strip()
)
SHARP_BOOK = os.getenv("SHARP_BOOK") or None          # e.g. "pinnacle" if your plan has it
DEVIG_METHOD = os.getenv("DEVIG_METHOD", "power")
DEFAULT_STAKE = float(os.getenv("DEFAULT_STAKE", "10"))
CACHE_TTL = int(os.getenv("CACHE_TTL", "120"))
DEMO_MODE = os.getenv("DEMO_MODE", "") not in ("", "0", "false", "False")
# Props and other per-event markets cost a credit per market per game. Live
# calls to them are refused unless you switch this on. Demo mode ignores it.
ENABLE_PROPS = os.getenv("ENABLE_PROPS", "") not in ("", "0", "false", "False")
PROPS_MAX_MARKETS = int(os.getenv("PROPS_MAX_MARKETS", "5"))
# Line movement: live fetches are written to a local SQLite file (gitignored).
RECORD_HISTORY = os.getenv("RECORD_HISTORY", "1") not in ("", "0", "false", "False")
HISTORY_DB = os.getenv("HISTORY_DB") or history.DEFAULT_DB

log = logging.getLogger("betting_desk")
_history: history.HistoryStore | None = None


def history_store() -> history.HistoryStore:
    global _history
    if _history is None or _history.path != HISTORY_DB:
        _history = history.HistoryStore(HISTORY_DB)
    return _history


def _record(league: str, rows: list[dict]) -> dict:
    """Write a snapshot. Never lets a disk problem break the route that paid for the data."""
    if not RECORD_HISTORY or DEMO_MODE:
        return {"recorded": 0, "enabled": RECORD_HISTORY and not DEMO_MODE}
    try:
        return {"recorded": history_store().record(league, rows, _now()), "enabled": True}
    except Exception as e:     # disk full, read-only checkout, locked file...
        log.warning("history write failed: %s", e)
        return {"recorded": 0, "enabled": True, "error": str(e)}

_client: OddsAPI | None = None


def odds_client() -> OddsAPI:
    global _client
    if _client is None:
        key = os.getenv("ODDS_API_KEY", "")
        if not key:
            raise HTTPException(500, "ODDS_API_KEY is not set. Copy .env.example to .env.")
        _client = OddsAPI(key, cache_ttl=CACHE_TTL)
    return _client


def _league(league: str) -> str:
    lg = league.lower()
    if lg not in SPORT_KEYS:
        raise HTTPException(400, f"Unknown league '{league}'. Try one of: {', '.join(SPORT_KEYS)}.")
    return lg


_EVENT_ID = re.compile(r"^[A-Za-z0-9_-]{1,64}$")


def _event_id(event_id: str) -> str:
    """Odds API ids are hex; demo ids are slugs. Anything else never reaches a URL path."""
    if not _EVENT_ID.match(event_id or ""):
        raise HTTPException(400, "event_id must be letters, digits, '-' or '_'.")
    return event_id


def _csv(v: str) -> tuple[str, ...]:
    return tuple(x.strip() for x in v.split(",") if x.strip())


def _demo_label(league: str) -> str:
    d = demo.demo_league(league)
    return d["label"] if d else "DEMO MODE (no fixture for this league)"


# ---------------- catalog (free, no network) ----------------

@app.get("/api/sports")
def sports() -> dict:
    """Every league the desk knows, with its markets and demo coverage."""
    return {
        "demo_mode": DEMO_MODE,
        "props_enabled": ENABLE_PROPS or DEMO_MODE,
        "props_max_markets": PROPS_MAX_MARKETS,
        "sports": [{
            "league": lg,
            "odds_api_key": key,
            "game_markets": list(GAME_MARKETS),
            "prop_markets": PROP_MARKETS.get(lg, []),
            "default_prop_markets": DEFAULT_PROP_MARKETS.get(lg, []),
            "demo_fixture": demo.demo_league(lg) is not None,
        } for lg, key in SPORT_KEYS.items()],
    }


# ---------------- schedule / context (free) ----------------

@app.get("/api/schedule/{league}")
def schedule(league: str, date: str | None = None,
             week: int | None = Query(None, description="NFL/NCAAF week number")) -> dict:
    """ESPN scoreboard. Free, no quota. date is YYYY-MM-DD."""
    league = _league(league)
    if DEMO_MODE:
        d = demo.demo_league(league)
        games = list(d["espn"]) if d else []
        return {"league": league, "count": len(games), "games": games,
                "source": _demo_label(league), "fetched_at": _now()}
    try:
        d = dt.date.fromisoformat(date) if date else None
    except ValueError:
        raise HTTPException(400, "date must be YYYY-MM-DD")
    try:
        games = espn.scoreboard(league, d, week=week)
    except Exception as e:
        raise HTTPException(502, f"ESPN request failed: {e}")
    return {"league": league, "count": len(games), "games": games,
            "source": "ESPN (free)", "fetched_at": _now()}


@app.get("/api/standings/{league}")
def get_standings(league: str) -> dict:
    league = _league(league)
    if DEMO_MODE:
        return {"league": league, "standings": _demo_standings(league),
                "source": _demo_label(league), "fetched_at": _now()}
    try:
        return {"league": league, "standings": espn.standings(league),
                "source": "ESPN (free)", "fetched_at": _now()}
    except Exception as e:
        raise HTTPException(502, f"ESPN request failed: {e}")


@app.get("/api/injuries/{league}")
def get_injuries(league: str) -> dict:
    league = _league(league)
    if DEMO_MODE:
        rows = list(demo.INJURIES_FIXTURE.get(league, []))
        source = "DEMO FIXTURE (synthetic injuries)"
    else:
        try:
            rows = espn.injuries(league)
        except Exception as e:
            raise HTTPException(502, f"ESPN request failed: {e}")
        source = "ESPN (free)"
    return {"league": league, "count": len(rows), "injuries": rows,
            "note": "Empty is a normal answer; ESPN injury coverage varies by league.",
            "source": source, "fetched_at": _now()}


def _demo_standings(league: str) -> list[dict]:
    """Records straight off the demo slate. Nothing derived beyond W-L."""
    d = demo.demo_league(league)
    rows: dict[str, dict] = {}
    for g in (d["espn"] if d else []):
        for t in (g["home"], g["away"]):
            rec = (t.get("record") or "").split("-")
            rows[t["name"]] = {
                "abbr": t["abbr"], "name": t["name"],
                "wins": float(rec[0]) if len(rec) > 1 else None,
                "losses": float(rec[1]) if len(rec) > 1 else None,
                "win_pct": None, "point_diff": None, "group": None,
            }
    return list(rows.values())


# ---------------- the board (costs credits) ----------------

@app.get("/api/board/{league}")
def board(
    league: str,
    stake: float = Query(default=None, description="Dollar stake for EV"),
    markets: str = Query("h2h,spreads,totals", description="Any of h2h, spreads, totals"),
    books: str = Query("", description="Comma-separated book keys; blank = all US books"),
    min_edge: float = Query(0.0, description="Only list plays above this EV per dollar"),
    method: str = Query(default=None, description="power | multiplicative | additive"),
) -> dict:
    """
    The refresh button. Pulls ESPN's slate and the odds feed, joins them,
    de-vigs every market and reports where your books beat fair value.

    Cost: len(markets) credits when scoped to up to 10 books, otherwise
    len(markets) x regions.
    """
    league = _league(league)
    mk = _csv(markets)
    bad = [m for m in mk if m not in GAME_MARKETS]
    if bad or not mk:
        raise HTTPException(
            400, f"Board markets are {', '.join(GAME_MARKETS)}; got {', '.join(bad) or 'none'}. "
                 "Player props and other per-event markets live at /api/props and /api/markets.")
    if method and method not in M.DEVIG_METHODS:
        raise HTTPException(400, f"method must be one of {', '.join(M.DEVIG_METHODS)}")

    if DEMO_MODE:
        d = demo.demo_league(league)
        odds_games = normalise_game_lines(d["odds"]) if d else []
        espn_games = list(d["espn"]) if d else []
        client = None
        history_info = {"recorded": 0, "enabled": False}
    else:
        client = odds_client()
        bk = _csv(books) or None
        try:
            raw = client.odds(league, markets=mk, bookmakers=bk)
        except Exception as e:
            raise HTTPException(502, f"Odds API request failed: {e}")
        odds_games = normalise_game_lines(raw)
        history_info = _record(league, history.rows_from_game_lines(odds_games))
        try:
            espn_games = espn.scoreboard(league)
        except Exception:
            espn_games = []   # odds alone still beat nothing

    result = build_board(
        espn_games, odds_games,
        your_books=YOUR_BOOKS,
        stake=stake if stake is not None else DEFAULT_STAKE,
        method=method or DEVIG_METHOD,
        sharp_book=SHARP_BOOK,
        min_edge=min_edge,
        markets=mk,
    )
    result["league"] = league
    result["markets"] = list(mk)
    result["usage"] = client.usage.to_dict() if client else {"demo": True}
    result["history"] = history_info
    result["sources"] = {
        "schedule": _demo_label(league) if DEMO_MODE
                    else ("ESPN (free)" if espn_games else "unavailable"),
        "odds": _demo_label(league) if DEMO_MODE else "The Odds API",
    }
    if DEMO_MODE:
        result["demo_mode"] = True
        if demo.demo_league(league) is None:
            result["note"] = f"No demo fixture for {league}. Demo covers: {', '.join(demo.DEMO_LEAGUES)}."
    return result


@app.get("/api/board")
def board_by_query(
    sport: str = Query(..., description="League key, e.g. nfl"),
    stake: float = Query(default=None),
    markets: str = Query("h2h,spreads,totals"),
    books: str = Query(""),
    min_edge: float = Query(0.0),
    method: str = Query(default=None),
) -> dict:
    """Same as /api/board/{league}, with the league as ?sport=."""
    return board(sport, stake=stake, markets=markets, books=books,
                 min_edge=min_edge, method=method)


@app.get("/api/events/{league}")
def events(league: str) -> dict:
    """Odds API event ids for a league - what /api/props needs. Free."""
    league = _league(league)
    if DEMO_MODE:
        d = demo.demo_league(league)
        raw = d["odds"] if d else []
        source, usage_ = _demo_label(league), {"demo": True}
    else:
        client = odds_client()
        try:
            raw = client.events(league)
        except Exception as e:
            raise HTTPException(502, f"Odds API request failed: {e}")
        source, usage_ = "The Odds API (free endpoint)", client.usage.to_dict()
    rows = [{
        "odds_id": e.get("id"),
        "home": e.get("home_team"),
        "away": e.get("away_team"),
        "start_utc": e.get("commence_time"),
        "demo_props": DEMO_MODE and e.get("id") in demo.PROPS_FIXTURE,
    } for e in raw]
    return {"league": league, "count": len(rows), "events": rows,
            "source": source, "usage": usage_, "fetched_at": _now()}


def _event_markets(
    league: str, event_id: str, mk: tuple[str, ...], books: str,
    stake: float | None, min_edge: float, method: str | None, dry_run: bool,
) -> dict:
    """Shared body of /api/props and /api/markets."""
    event_id = _event_id(event_id)
    if not mk:
        raise HTTPException(400, f"No markets asked for and no defaults configured for {league}.")
    if len(mk) > PROPS_MAX_MARKETS:
        raise HTTPException(
            400, f"{len(mk)} markets asked for; the cap is {PROPS_MAX_MARKETS} per call "
                 "(each one is a credit). Raise PROPS_MAX_MARKETS if you mean it.")
    if method and method not in M.DEVIG_METHODS:
        raise HTTPException(400, f"method must be one of {', '.join(M.DEVIG_METHODS)}")
    bk = _csv(books) or None
    cost = estimate_cost(mk, bk)
    base = {"league": league, "event_id": event_id, "markets": list(mk),
            "estimated_cost": 0 if DEMO_MODE else cost}

    if dry_run:
        return {**base, "dry_run": True,
                "note": "Nothing fetched. estimated_cost is an upper bound in credits."}

    if DEMO_MODE:
        blob = demo.demo_event_markets(event_id, mk)
        if blob is None:
            raise HTTPException(
                404, f"No demo props for event {event_id}. "
                     f"Demo events with props: {', '.join(demo.PROPS_FIXTURE)}.")
        usage_, source = {"demo": True}, demo.PROPS_LABEL
    else:
        if not ENABLE_PROPS:
            raise HTTPException(
                403, f"Per-event markets are off. This call would cost up to {cost} credits. "
                     "Set ENABLE_PROPS=1 to allow them, or pass dry_run=true to price it.")
        client = odds_client()
        try:
            blob = client.event_odds(league, event_id, markets=mk, bookmakers=bk)
        except Exception as e:
            raise HTTPException(502, f"Odds API request failed: {e}")
        usage_, source = client.usage.to_dict(), "The Odds API"
        _record(league, history.rows_from_event_blob(blob))

    res = analyse_event_markets(
        blob, YOUR_BOOKS,
        stake=stake if stake is not None else DEFAULT_STAKE,
        method=method or DEVIG_METHOD, sharp_book=SHARP_BOOK, min_edge=min_edge,
    )
    out = {**base, **res, "count": len(res["props"]), "usage": usage_,
           "source": source, "fetched_at": _now()}
    if DEMO_MODE:
        out["demo_mode"] = True
    return out


@app.get("/api/props/{league}/{event_id}")
def props(
    league: str,
    event_id: str,
    markets: str = Query(default="", description="Prop market keys; blank = the league's short default list"),
    books: str = Query("", description="Book keys; blank = US region (same cost for up to 10 books)"),
    stake: float = Query(default=None),
    min_edge: float = Query(0.0),
    method: str = Query(default=None),
    dry_run: bool = Query(False, description="Price the call in credits without making it"),
) -> dict:
    """
    Player props for one event, de-vigged, with plays above min_edge.

    This is the expensive endpoint and it is opt-in: live calls need
    ENABLE_PROPS=1. One call costs one credit per market, so five markets
    on one game is five credits. Pull it per game, not across a slate.
    """
    league = _league(league)
    mk = _csv(markets) or tuple(DEFAULT_PROP_MARKETS.get(league, []))
    return _event_markets(league, event_id, mk, books, stake, min_edge, method, dry_run)


@app.get("/api/props")
def props_by_query(
    sport: str = Query(...),
    event_id: str = Query(...),
    markets: str = Query(""),
    books: str = Query(""),
    stake: float = Query(default=None),
    min_edge: float = Query(0.0),
    method: str = Query(default=None),
    dry_run: bool = Query(False),
) -> dict:
    """Same as /api/props/{league}/{event_id}, with ?sport=&event_id=."""
    return props(sport, event_id, markets=markets, books=books, stake=stake,
                 min_edge=min_edge, method=method, dry_run=dry_run)


@app.get("/api/markets/{league}/{event_id}")
def event_markets(
    league: str,
    event_id: str,
    markets: str = Query(..., description="Any per-event market keys, e.g. totals_h1,player_points"),
    books: str = Query(""),
    stake: float = Query(default=None),
    min_edge: float = Query(0.0),
    method: str = Query(default=None),
    dry_run: bool = Query(False),
) -> dict:
    """
    Any per-event market The Odds API serves (period lines, alternates,
    props), paired and de-vigged. No defaults: you name what you pay for.
    Same opt-in and cost rules as /api/props.
    """
    league = _league(league)
    return _event_markets(league, event_id, _csv(markets), books, stake,
                          min_edge, method, dry_run)


# ---------------- line movement (free, reads what was already fetched) ----------------

@app.get("/api/history/{league}/{event_id}")
def price_history(
    league: str,
    event_id: str,
    market: str = Query("h2h", description="h2h, spreads, totals, or any per-event market key"),
    book: str = Query("", description="One book key; blank = every book"),
    player: str = Query("", description="Props: only this player's lines"),
    method: str = Query(default=None),
) -> dict:
    """
    Price history for one market in one event, per book, plus the fair
    price over time for game markets. Costs nothing: it only reads snapshots
    the board and markets routes stored when they fetched. Demo mode serves
    a synthetic history for every demo game.
    """
    league = _league(league)
    event_id = _event_id(event_id)
    if method and method not in M.DEVIG_METHODS:
        raise HTTPException(400, f"method must be one of {', '.join(M.DEVIG_METHODS)}")
    if DEMO_MODE:
        rows = demo.demo_history_rows(event_id)
        if rows is None:
            raise HTTPException(404, f"No demo game with id {event_id}. See /api/events/{league}.")
        source = demo.HISTORY_LABEL
    else:
        try:
            rows = history_store().rows(event_id, market)
        except Exception as e:
            raise HTTPException(500, f"History store unreadable: {e}")
        source = "local snapshots of The Odds API"
    rows = [r for r in rows if r.get("league") in (None, league)]
    res = history.build_history(rows, market, book=book or None, player=player or None,
                                method=method or DEVIG_METHOD, sharp_book=SHARP_BOOK)
    out = {"league": league, "event_id": event_id, **res, "source": source,
           "recording": RECORD_HISTORY and not DEMO_MODE, "fetched_at": _now()}
    if not res["snapshots"]:
        out["note"] = ("No snapshots for this market yet. History is recorded each time "
                       "the board (or a props/markets call) fetches this event.")
    if DEMO_MODE:
        out["demo_mode"] = True
    return out


# ---------------- parlay pricing (free, no network) ----------------

class Leg(BaseModel):
    label: str
    american: float

    @field_validator("american")
    @classmethod
    def _real_price(cls, v: float) -> float:
        if -100 < v < 100:
            raise ValueError("american odds are <= -100 or >= +100")
        return v

    fair_prob: float | None = Field(default=None, ge=0.0, le=1.0)
    game_id: str | None = None


class ParlayRequest(BaseModel):
    legs: list[Leg]
    stake: float = Field(default=10.0, gt=0)


@app.post("/api/parlay")
def price_parlay(req: ParlayRequest) -> dict:
    """
    Price a slip and report EV honestly.

    If any leg has no fair_prob, EV comes back null rather than estimated.
    Same-game slips are flagged: the independent math overstates them.
    """
    legs = [l.model_dump() for l in req.legs]
    report = M.parlay_report(legs, req.stake)

    report["leg_detail"] = [{
        "label": l["label"],
        "american": l["american"],
        "decimal": round(M.american_to_decimal(l["american"]), 4),
        "breakeven_prob": round(M.breakeven_prob(l["american"]), 5),
        "fair_prob": l["fair_prob"],
        "ev_per_dollar": (round(M.ev_percent(l["american"], l["fair_prob"]), 5)
                          if l["fair_prob"] is not None else None),
    } for l in legs]

    # what the same money does as singles instead
    if legs:
        per = req.stake / len(legs)
        singles_ev = None
        if all(l["fair_prob"] is not None for l in legs):
            singles_ev = round(sum(
                M.expected_value(per, l["american"], l["fair_prob"]) for l in legs
            ), 2)
        report["as_singles"] = {
            "stake_each": round(per, 2),
            "total_ev_dollars": singles_ev,
            "comment": "Same total risk, split across the legs as separate bets.",
        }
    return report


@app.get("/api/convert")
def convert(american: float | None = None, decimal: float | None = None,
            prob: float | None = None) -> dict:
    """Odds converter. Pass any one of american, decimal or prob."""
    if american is not None:
        if -100 < american < 100:
            raise HTTPException(400, "american odds are <= -100 or >= +100.")
        d = M.american_to_decimal(american)
        p = M.american_to_implied(american)
    elif decimal is not None:
        if decimal <= 1.0:
            raise HTTPException(400, "decimal odds must be greater than 1.")
        d, p = decimal, 1.0 / decimal
        american = M.decimal_to_american(decimal)
    elif prob is not None:
        if not 0.0 < prob < 1.0:
            raise HTTPException(400, "prob must be strictly between 0 and 1.")
        p, d = prob, 1.0 / prob
        american = M.implied_to_american(prob)
    else:
        raise HTTPException(400, "Pass american, decimal or prob.")
    return {"american": int(american), "decimal": round(d, 4),
            "implied_prob": round(p, 5),
            "breakeven_win_rate": f"{p * 100:.2f}%"}


@app.get("/api/usage")
def usage() -> dict:
    if DEMO_MODE and not os.getenv("ODDS_API_KEY"):
        return {"demo": True, "note": "Demo mode spends no credits."}
    try:
        return odds_client().usage.to_dict()
    except HTTPException:
        return {"error": "ODDS_API_KEY not set"}


@app.get("/api/health")
def health() -> dict:
    return {
        "ok": True,
        "your_books": list(YOUR_BOOKS),
        "sharp_book": SHARP_BOOK,
        "devig_method": DEVIG_METHOD,
        "odds_key_present": bool(os.getenv("ODDS_API_KEY")),
        "demo_mode": DEMO_MODE,
        "props_enabled": ENABLE_PROPS,
        "history_recording": RECORD_HISTORY and not DEMO_MODE,
        "time": _now(),
    }


def _now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat()


# ---------------- UI ----------------

if os.path.isdir(STATIC_DIR):
    app.mount("/static", StaticFiles(directory=STATIC_DIR), name="static")

    @app.get("/")
    def index() -> Any:
        return FileResponse(os.path.join(STATIC_DIR, "index.html"))
