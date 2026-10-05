"""
Failure-mode tests: the board must degrade, never 500, when ESPN or The Odds
API time out, error, rate-limit, or send malformed/partial data.

Every upstream call is mocked with respx; nothing touches the network.
Run: python3 -m pytest tests/
"""
import os
import sys

import httpx
import pytest
import respx
from fastapi.testclient import TestClient

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from app import main  # noqa: E402
from app.services import board as board_mod  # noqa: E402
from app.sources import espn, odds_api  # noqa: E402

ODDS_URL = f"{odds_api.BASE}/sports/baseball_mlb/odds"
ESPN_URL = f"{espn.BASE}/baseball/mlb/scoreboard"
ESPN_INJ_URL = f"{espn.BASE}/baseball/mlb/injuries"
CREDIT_HEADERS = {"x-requests-remaining": "412", "x-requests-used": "88", "x-requests-last": "3"}


def _book(key, home_px, away_px):
    return {"key": key, "markets": [
        {"key": "h2h", "outcomes": [{"name": "Tampa Bay Rays", "price": home_px},
                                    {"name": "Athletics", "price": away_px}]},
        {"key": "totals", "outcomes": [{"name": "Over", "price": -110, "point": 8.5},
                                       {"name": "Under", "price": -110, "point": 8.5}]}]}


ODDS_OK = [{
    "id": "evt_ath_tb", "sport_key": "baseball_mlb", "commence_time": "2026-09-15T22:40:00Z",
    "home_team": "Tampa Bay Rays", "away_team": "Athletics",
    "bookmakers": [_book("fanduel", -230, 190), _book("draftkings", -215, 180),
                   _book("betmgm", -225, 185)],
}]

ESPN_OK = {"events": [{
    "id": "401", "name": "Athletics at Tampa Bay Rays", "shortName": "ATH @ TB",
    "date": "2026-09-15T22:40Z",
    "status": {"type": {"state": "pre", "detail": "6:40 PM ET", "completed": False}},
    "competitions": [{"venue": {"fullName": "Steinbrenner Field"}, "competitors": [
        {"homeAway": "home", "score": "0", "team": {"abbreviation": "TB", "displayName": "Tampa Bay Rays"}},
        {"homeAway": "away", "score": "0", "team": {"abbreviation": "ATH", "displayName": "Athletics"}},
    ]}],
}, {
    "id": "402", "name": "Seattle Mariners at Los Angeles Angels", "date": "2026-09-16T01:38Z",
    "status": {"type": {"state": "pre"}},
    "competitions": [{"competitors": [
        {"homeAway": "home", "team": {"abbreviation": "LAA", "displayName": "Los Angeles Angels"}},
        {"homeAway": "away", "team": {"abbreviation": "SEA", "displayName": "Seattle Mariners"}},
    ]}],
}]}


@pytest.fixture
def client(monkeypatch):
    monkeypatch.setattr(main, "DEMO_MODE", False)
    monkeypatch.setenv("ODDS_API_KEY", "test-key")
    monkeypatch.setattr(main, "_client", None)
    with respx.mock(assert_all_called=False) as mock:
        yield TestClient(main.app), mock
    main._client = None


def _board(tc):
    r = tc.get("/api/board/mlb")
    assert r.status_code == 200, r.text
    return r.json()


def _kinds(body, source):
    return [e["kind"] for e in body["errors"] if e["source"] == source]


# ---------------- happy path baseline ----------------

def test_healthy_board_has_no_errors(client):
    tc, mock = client
    mock.get(ODDS_URL).respond(200, json=ODDS_OK, headers=CREDIT_HEADERS)
    mock.get(ESPN_URL).respond(200, json=ESPN_OK)
    body = _board(tc)
    assert body["errors"] == []
    assert body["degraded"] is False
    assert body["summary"]["games_total"] == 2
    assert body["summary"]["games_with_odds"] == 1
    assert body["usage"]["credits_remaining"] == 412


# ---------------- The Odds API failures ----------------

@pytest.mark.parametrize("respond,kind", [
    (dict(side_effect=httpx.ReadTimeout("slow")), "timeout"),
    (dict(side_effect=httpx.ConnectError("refused")), "network"),
    (dict(status_code=429, headers={"retry-after": "30", **CREDIT_HEADERS}), "rate_limit"),
    (dict(status_code=401), "auth"),
    (dict(status_code=500), "upstream"),
    (dict(status_code=503, text="<html>down</html>"), "upstream"),
    (dict(status_code=200, text="<html>not json</html>"), "malformed"),
])
def test_odds_failure_keeps_schedule(client, respond, kind):
    tc, mock = client
    route = mock.get(ODDS_URL)
    if "side_effect" in respond:
        route.side_effect = respond["side_effect"]
    else:
        route.respond(**respond)
    mock.get(ESPN_URL).respond(200, json=ESPN_OK)

    body = _board(tc)
    assert _kinds(body, "odds") == [kind]
    assert body["degraded"] is True
    assert body["summary"]["games_total"] == 2          # ESPN slate still there
    assert body["summary"]["games_with_odds"] == 0
    assert body["plays"] == []
    assert body["sources"]["odds"] == "unavailable"


def test_odds_429_message_and_credits_survive(client):
    tc, mock = client
    mock.get(ODDS_URL).respond(429, headers={"retry-after": "30", **CREDIT_HEADERS})
    mock.get(ESPN_URL).respond(200, json=ESPN_OK)
    body = _board(tc)
    err = body["errors"][0]
    assert "429" in err["message"] and "30s" in err["message"]
    assert body["usage"]["credits_remaining"] == 412


def test_odds_wrong_top_level_shape(client):
    """API sometimes answers with an object ({"message": ...}) instead of a list."""
    tc, mock = client
    mock.get(ODDS_URL).respond(200, json={"message": "Unknown sport"})
    mock.get(ESPN_URL).respond(200, json=ESPN_OK)
    body = _board(tc)
    assert body["summary"]["games_total"] == 2
    assert body["summary"]["games_with_odds"] == 0


def test_odds_partial_and_malformed_rows(client):
    """Junk rows are dropped; the good book/market survives and is priced."""
    tc, mock = client
    junk = [
        "not-an-event",
        {"id": "no_teams", "bookmakers": [_book("fanduel", -110, -110)]},
        {**ODDS_OK[0], "bookmakers": [
            *ODDS_OK[0]["bookmakers"],
            {"key": "caesars", "markets": "oops"},                       # markets not a list
            {"key": "pointsbet", "markets": [{"key": "h2h", "outcomes": [
                {"name": "Tampa Bay Rays", "price": "abc"},              # non-numeric price
                {"name": "Athletics", "price": 0}]}]},                   # impossible price
            {"markets": []},                                             # no key
            None,
            {"key": "bovada", "markets": [{"key": "spreads", "outcomes": [
                {"name": "Tampa Bay Rays", "price": -110, "point": "x"}]}]},  # one side only
        ]},
    ]
    mock.get(ODDS_URL).respond(200, json=junk)
    mock.get(ESPN_URL).respond(200, json=ESPN_OK)
    body = _board(tc)
    assert body["errors"] == []
    tb = next(g for g in body["games"] if g["espn_id"] == "401")
    assert tb["has_odds"]
    assert tb["markets"]["h2h"]["book_count"] == 3       # only the three clean books
    assert tb["markets"]["spreads"] is None              # half a market is absent, not guessed
    assert tb["markets"]["totals"]["book_count"] == 3


def test_missing_api_key_is_not_a_500(client, monkeypatch):
    tc, mock = client
    monkeypatch.delenv("ODDS_API_KEY")
    mock.get(ESPN_URL).respond(200, json=ESPN_OK)
    body = _board(tc)
    assert _kinds(body, "odds") == ["config"]
    assert body["summary"]["games_total"] == 2


def test_unknown_league_is_not_a_500(client):
    # an unknown league is the caller's mistake: a clear 400, never a 500
    tc, _ = client
    r = tc.get("/api/board/curling")
    assert r.status_code == 400
    assert "curling" in r.json()["detail"]


def test_stale_cache_served_when_feed_fails(client, monkeypatch):
    tc, mock = client
    route = mock.get(ODDS_URL)
    route.respond(200, json=ODDS_OK, headers=CREDIT_HEADERS)
    mock.get(ESPN_URL).respond(200, json=ESPN_OK)
    assert _board(tc)["stale_odds"] is False

    main._client.cache_ttl = 0                           # expire the cache
    route.side_effect = httpx.ReadTimeout("slow")
    body = _board(tc)
    assert body["stale_odds"] is True
    assert body["degraded"] is True
    assert body["summary"]["games_with_odds"] == 1       # old prices, not no prices
    assert "cached" in body["sources"]["odds"]


# ---------------- ESPN failures ----------------

@pytest.mark.parametrize("respond,kind", [
    (dict(side_effect=httpx.ConnectTimeout("slow")), "timeout"),
    (dict(side_effect=httpx.ConnectError("dns")), "network"),
    (dict(status_code=429), "rate_limit"),
    (dict(status_code=502), "upstream"),
    (dict(status_code=200, text="{truncated"), "malformed"),
    (dict(status_code=200, json=["not", "a", "dict"]), "malformed"),
])
def test_espn_failure_keeps_odds(client, respond, kind):
    tc, mock = client
    mock.get(ODDS_URL).respond(200, json=ODDS_OK, headers=CREDIT_HEADERS)
    route = mock.get(ESPN_URL)
    if "side_effect" in respond:
        route.side_effect = respond["side_effect"]
    else:
        route.respond(**respond)

    body = _board(tc)
    assert _kinds(body, "schedule") == [kind]
    assert body["summary"]["games_total"] == 1           # odds-only game kept
    assert body["summary"]["games_with_odds"] == 1
    assert body["sources"]["schedule"] == "unavailable"


def test_espn_partial_payload(client):
    tc, mock = client
    mock.get(ODDS_URL).respond(200, json=ODDS_OK, headers=CREDIT_HEADERS)
    mock.get(ESPN_URL).respond(200, json={"events": [
        ESPN_OK["events"][0],
        "garbage",
        {"id": "9", "competitions": "nope", "status": None},
        {"id": "10", "competitions": [{"competitors": [None, {"homeAway": "home", "team": None}]}]},
    ]})
    body = _board(tc)
    assert body["errors"] == []
    assert any(g["espn_id"] == "401" and g["has_odds"] for g in body["games"])


def test_both_sources_down(client):
    tc, mock = client
    mock.get(ODDS_URL).respond(429)
    mock.get(ESPN_URL).side_effect = httpx.ReadTimeout("slow")
    body = _board(tc)
    assert sorted(e["source"] for e in body["errors"]) == ["odds", "schedule"]
    assert body["games"] == [] and body["plays"] == []


def test_schedule_endpoint_maps_errors(client):
    tc, mock = client
    mock.get(ESPN_URL).respond(429)
    assert tc.get("/api/schedule/mlb").status_code == 429
    assert tc.get("/api/schedule/mlb?date=not-a-date").status_code == 400


def test_injuries_never_error(client):
    tc, mock = client
    mock.get(ESPN_INJ_URL).side_effect = httpx.ReadTimeout("slow")
    r = tc.get("/api/injuries/mlb")
    assert r.status_code == 200 and r.json()["injuries"] == []
    assert tc.get("/api/injuries/curling").status_code == 400   # bad input, not a crash


def test_props_rate_limit_is_429(client, monkeypatch):
    tc, mock = client
    monkeypatch.setattr(main, "ENABLE_PROPS", True)   # live props are opt-in
    mock.get(url__regex=r".*/events/abc/odds").respond(429)
    assert tc.get("/api/props/mlb/abc").status_code == 429


# ---------------- board builder isolation ----------------

def test_one_broken_market_does_not_sink_board(monkeypatch):
    real = board_mod.analyse_market

    def flaky(prices, labels, *a, **kw):
        if labels == ["Over", "Under"]:
            raise ZeroDivisionError("bad totals")
        return real(prices, labels, *a, **kw)

    monkeypatch.setattr(board_mod, "analyse_market", flaky)
    games = odds_api.normalise_game_lines(ODDS_OK)
    out = board_mod.build_board([], games, your_books=("fanduel", "draftkings"))
    g = out["games"][0]
    assert g["markets"]["totals"] is None
    assert g["markets"]["h2h"] is not None
    assert any("totals" in w for w in out["warnings"])


def test_usage_keeps_last_known_credits_on_headerless_error():
    u = odds_api.Usage()
    u.update(httpx.Headers(CREDIT_HEADERS))
    u.update(httpx.Headers({}))
    assert u.remaining == 412 and u.used == 88
