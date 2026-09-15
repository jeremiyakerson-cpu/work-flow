"""
Betting Desk API.

Run:  uvicorn app.main:app --reload
Then: http://127.0.0.1:8000  (UI)  /docs  (API explorer)
"""
from __future__ import annotations

import datetime as dt
import os
from typing import Any

from fastapi import FastAPI, HTTPException, Query
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel, Field

from .sources import espn
from .sources.odds_api import OddsAPI, normalise_game_lines, normalise_props, PROP_MARKETS
from .services.board import build_board
from .demo import ODDS_FIXTURE, ESPN_FIXTURE
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

_client: OddsAPI | None = None


def odds_client() -> OddsAPI:
    global _client
    if _client is None:
        key = os.getenv("ODDS_API_KEY", "")
        if not key:
            raise HTTPException(500, "ODDS_API_KEY is not set. Copy .env.example to .env.")
        _client = OddsAPI(key, cache_ttl=CACHE_TTL)
    return _client


# ---------------- schedule / context (free) ----------------

@app.get("/api/schedule/{league}")
def schedule(league: str, date: str | None = None) -> dict:
    """ESPN scoreboard. Free, no quota. date is YYYY-MM-DD."""
    d = dt.date.fromisoformat(date) if date else None
    try:
        games = espn.scoreboard(league, d)
    except Exception as e:
        raise HTTPException(502, f"ESPN request failed: {e}")
    return {"league": league, "count": len(games), "games": games,
            "source": "ESPN (free)", "fetched_at": _now()}


@app.get("/api/standings/{league}")
def get_standings(league: str) -> dict:
    try:
        return {"league": league, "standings": espn.standings(league),
                "source": "ESPN (free)", "fetched_at": _now()}
    except Exception as e:
        raise HTTPException(502, f"ESPN request failed: {e}")


@app.get("/api/injuries/{league}")
def get_injuries(league: str) -> dict:
    rows = espn.injuries(league)
    return {"league": league, "count": len(rows), "injuries": rows,
            "note": "Empty is a normal answer; ESPN injury coverage varies by league.",
            "source": "ESPN (free)", "fetched_at": _now()}


# ---------------- the board (costs credits) ----------------

@app.get("/api/board/{league}")
def board(
    league: str,
    stake: float = Query(default=None, description="Dollar stake for EV"),
    markets: str = Query("h2h,spreads,totals"),
    books: str = Query("", description="Comma-separated book keys; blank = all US books"),
    min_edge: float = Query(0.0, description="Only list plays above this EV per dollar"),
    method: str = Query(default=None, description="power | multiplicative | additive"),
) -> dict:
    """
    The refresh button. Pulls ESPN's slate and the odds feed, joins them,
    de-vigs every market and reports where your books beat fair value.

    Cost: len(markets) credits when scoped to books, otherwise
    len(markets) x regions.
    """
    if DEMO_MODE:
        odds_games = normalise_game_lines(ODDS_FIXTURE)
        espn_games = list(ESPN_FIXTURE)
        client = None
    else:
        client = odds_client()
        mk = tuple(m.strip() for m in markets.split(",") if m.strip())
        bk = tuple(b.strip() for b in books.split(",") if b.strip()) or None
        try:
            raw = client.odds(league, markets=mk, bookmakers=bk)
        except Exception as e:
            raise HTTPException(502, f"Odds API request failed: {e}")
        odds_games = normalise_game_lines(raw)
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
    )
    result["league"] = league
    result["usage"] = client.usage.to_dict() if client else {"demo": True}
    result["sources"] = {
        "schedule": "DEMO FIXTURE (frozen Sep 15 2026)" if DEMO_MODE
                    else ("ESPN (free)" if espn_games else "unavailable"),
        "odds": "DEMO FIXTURE (frozen Sep 15 2026)" if DEMO_MODE else "The Odds API",
    }
    if DEMO_MODE:
        result["demo_mode"] = True
    return result


@app.get("/api/props/{league}/{event_id}")
def props(
    league: str,
    event_id: str,
    markets: str = Query(default=""),
    books: str = Query(""),
    stake: float = Query(default=None),
) -> dict:
    """
    Player props for one event, with every book's price side by side.

    This is the expensive endpoint. One call costs one credit per market,
    so five markets on one game is five credits. Pull it per game, not
    across a slate, unless you mean to.
    """
    client = odds_client()
    mk = tuple(m.strip() for m in markets.split(",") if m.strip()) \
         or tuple(PROP_MARKETS.get(league, []))
    if not mk:
        raise HTTPException(400, f"No prop markets configured for {league}.")
    bk = tuple(b.strip() for b in books.split(",") if b.strip()) or YOUR_BOOKS

    try:
        blob = client.event_odds(league, event_id, markets=mk, bookmakers=bk)
    except Exception as e:
        raise HTTPException(502, f"Odds API request failed: {e}")

    rows = normalise_props(blob)
    s = stake if stake is not None else DEFAULT_STAKE

    # pair Over with Under per (market, player, point) so props can be de-vigged
    paired: dict[tuple, dict] = {}
    for r in rows:
        k = (r["market"], r["player"], r["point"])
        paired.setdefault(k, {"market": r["market"], "player": r["player"],
                              "point": r["point"], "over": {}, "under": {}})
        side = (r["side"] or "").lower()
        if side in ("over", "under"):
            paired[k][side] = r["prices"]
        else:
            paired[k].setdefault("other", {})[r["side"]] = r["prices"]

    out = []
    for v in paired.values():
        books_both = {b: [v["over"][b], v["under"][b]]
                      for b in set(v["over"]) & set(v["under"])
                      if v["over"][b] is not None and v["under"][b] is not None}
        analysis = None
        if books_both:
            from .services.board import analyse_market
            analysis = analyse_market(books_both, ["Over", "Under"],
                                      YOUR_BOOKS, s, DEVIG_METHOD, SHARP_BOOK)
        out.append({**v, "analysis": analysis,
                    "note": None if books_both else
                            "Only one side quoted - cannot strip vig on this prop."})

    out.sort(key=lambda r: (r["market"], r["player"] or ""))
    return {"league": league, "event_id": event_id, "markets": list(mk),
            "count": len(out), "props": out,
            "usage": client.usage.to_dict(), "fetched_at": _now()}


# ---------------- parlay pricing (free, no network) ----------------

class Leg(BaseModel):
    label: str
    american: float
    fair_prob: float | None = Field(default=None, ge=0.0, le=1.0)
    game_id: str | None = None


class ParlayRequest(BaseModel):
    legs: list[Leg]
    stake: float = 10.0


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
        d = M.american_to_decimal(american)
        p = M.american_to_implied(american)
    elif decimal is not None:
        d, p = decimal, 1.0 / decimal
        american = M.decimal_to_american(decimal)
    elif prob is not None:
        p, d = prob, 1.0 / prob
        american = M.implied_to_american(prob)
    else:
        raise HTTPException(400, "Pass american, decimal or prob.")
    return {"american": int(american), "decimal": round(d, 4),
            "implied_prob": round(p, 5),
            "breakeven_win_rate": f"{p * 100:.2f}%"}


@app.get("/api/usage")
def usage() -> dict:
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
