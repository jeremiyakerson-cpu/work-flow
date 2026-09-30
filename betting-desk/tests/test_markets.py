"""
Multi-sport coverage, spreads/totals line handling, props, and the routes
that expose them. Offline: demo fixtures and fakes only, no network, no key.

Run: python3 -m pytest tests/
"""
import os
import sys

import pytest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from fastapi.testclient import TestClient

from app import demo, main
from app.math_engine import push_possible, devig
from app.services.board import build_board, main_line, match_games
from app.services.props import analyse_event_markets, collect_event_markets
from app.sources import odds_api
from app.sources.odds_api import OddsAPI, estimate_cost, normalise_game_lines

BOOKS = ("fanduel", "draftkings")


def _board(league, **kw):
    d = demo.DEMO_LEAGUES[league]
    return build_board(d["espn"], normalise_game_lines(d["odds"]), your_books=BOOKS, **kw)


def _game(board, needle):
    return next(g for g in board["games"] if needle in g["name"])


# ---------------- new math ----------------

def test_push_possible_whole_numbers_only():
    assert push_possible(-3.0) and push_possible(44) and push_possible(0)
    assert not push_possible(-3.5) and not push_possible(221.5)
    assert not push_possible(None)


def test_estimate_cost_is_markets_times_regions():
    assert estimate_cost(["h2h", "spreads", "totals"]) == 3
    assert estimate_cost(["player_points"], regions="us,us2") == 2
    # each group of up to ten books bills as one region
    assert estimate_cost(["a", "b"], bookmakers=["fanduel", "draftkings"]) == 2
    assert estimate_cost(["a"], bookmakers=[f"b{i}" for i in range(11)]) == 2


def test_main_line_is_modal_then_most_balanced():
    q = {
        "a": [{"price": -110, "point": -3.5}, {"price": -110, "point": 3.5}],
        "b": [{"price": -120, "point": -3.0}, {"price": 100, "point": 3.0}],
        "c": [{"price": -105, "point": -3.5}, {"price": -115, "point": 3.5}],
    }
    assert main_line(q) == -3.5
    # one book each: the even-money market is the main line
    tie = {"a": q["a"], "b": q["b"]}
    assert main_line(tie) == -3.5
    assert main_line({}) is None


# ---------------- board: every demo league ----------------

@pytest.mark.parametrize("league", ["mlb", "nfl", "ncaaf", "nba", "nhl"])
def test_every_demo_league_builds_and_devigs(league):
    b = _board(league)
    assert b["summary"]["games_with_odds"] >= 2
    for g in b["games"]:
        if not g["has_odds"]:
            continue
        assert g["odds_id"]
        assert set(g["markets"]) == {"h2h", "spreads", "totals"}
        for m in g["markets"].values():
            if m:
                assert abs(sum(s["fair_prob"] for s in m["sides"]) - 1) < 1e-3


def test_off_line_book_is_excluded_not_averaged():
    g = _game(_board("nfl"), "Kansas City")
    sp = g["markets"]["spreads"]
    # DraftKings deals -3, the other three -3.5
    assert sp["sides"][0]["point"] == -3.5 and sp["sides"][1]["point"] == 3.5
    assert sp["book_count"] == 3
    assert "draftkings" not in sp["market_hold"]
    assert sp["off_line_books"] == {"draftkings": {"point": -3.0, "prices": [-120, 100]}}
    # so DK has no EV on a line it isn't dealing
    assert [y["book"] for y in sp["sides"][0]["your_books"]] == ["fanduel"]
    # BetMGM's total of 48 is off the 47.5 main line too
    assert g["markets"]["totals"]["off_line_books"]["betmgm"]["point"] == 48.0


def test_whole_number_lines_flag_push():
    b = _board("nfl")
    g = _game(b, "Dallas")
    assert all(s["push_possible"] for s in g["markets"]["spreads"]["sides"])
    assert all(s["push_possible"] for s in g["markets"]["totals"]["sides"])
    assert not any(s["push_possible"] for s in g["markets"]["h2h"]["sides"])
    kc = _game(b, "Kansas City")
    assert not any(s["push_possible"] for s in kc["markets"]["spreads"]["sides"])
    assert all("push_possible" in p and "odds_id" in p for p in b["plays"])


def test_nhl_puck_line_and_total():
    g = _game(_board("nhl"), "Toronto")
    assert g["markets"]["spreads"]["sides"][0]["point"] == 1.5
    tot = g["markets"]["totals"]
    assert tot["sides"][0]["point"] == 6.0 and tot["sides"][0]["push_possible"]
    assert "caesars" in tot["off_line_books"]


def test_markets_filter_leaves_other_keys_none():
    g = _game(_board("nba", markets=("totals",)), "Boston")
    assert g["markets"]["h2h"] is None and g["markets"]["spreads"] is None
    assert g["markets"]["totals"] is not None


def test_start_time_gate_stops_wrong_school_match():
    b = _board("ncaaf")
    assert _game(b, "RedHawks")["has_odds"] is False
    assert _game(b, "Hurricanes")["match_confidence"] > 0.9
    # same two names a week later: a perfect name match, but not this game
    later = {**demo.NCAAF_ESPN[2], "espn_id": "999", "start_utc": "2026-10-10T23:30:00Z"}
    odds = normalise_game_lines(demo.NCAAF_ODDS)
    merged = match_games([later], odds)
    assert merged[0]["odds"] is None
    assert match_games([demo.NCAAF_ESPN[2]], odds)[0]["odds"]["odds_id"] == "evt_miami_fsu"


def test_three_way_market_is_absent_not_wrong():
    ev = [{"id": "x", "commence_time": "2026-10-01T15:00:00Z",
           "home_team": "Arsenal", "away_team": "Chelsea", "bookmakers": [
               {"key": k, "markets": [{"key": "h2h", "outcomes": [
                   {"name": "Arsenal", "price": 120}, {"name": "Chelsea", "price": 230},
                   {"name": "Draw", "price": 250}]}]} for k in ("a", "b", "c")]}]
    espn = [{"espn_id": "1", "name": "Chelsea at Arsenal", "start_utc": "2026-10-01T15:00:00Z",
             "home": {"name": "Arsenal"}, "away": {"name": "Chelsea"}}]
    g = build_board(espn, normalise_game_lines(ev), your_books=("a",))["games"][0]
    assert g["has_odds"] and g["markets"]["h2h"] is None


def test_matching_ignores_unparseable_times():
    espn = [{"home": {"name": "A"}, "away": {"name": "B"}, "start_utc": None}]
    odds = [{"home": "A", "away": "B", "start_utc": "garbage"}]
    assert match_games(espn, odds)[0]["odds"] is not None


# ---------------- props ----------------

def test_props_pair_over_under_and_yes_no():
    res = analyse_event_markets(demo.PROPS_FIXTURE["evt_kc_buf"], BOOKS)
    rows = {(p["market"], p["player"], p["point"]): p for p in res["props"]}

    allen = rows[("player_pass_yds", "Josh Allen", 254.5)]
    assert allen["labels"] == ["Over", "Under"]
    assert allen["analysis"]["book_count"] == 4
    assert allen["over"]["fanduel"] == -115    # legacy shape kept
    fair = devig([-115, -115])                 # sanity: symmetric book is 50/50
    assert abs(fair[0] - 0.5) < 1e-9

    cook_td = rows[("player_anytime_td", "James Cook", None)]
    assert cook_td["labels"] == ["Yes", "No"]
    # FanDuel only hangs Yes, so it can't be in the de-vig
    assert cook_td["analysis"]["book_count"] == 3
    assert "fanduel" not in cook_td["analysis"]["market_hold"]

    kelce = rows[("player_anytime_td", "Travis Kelce", None)]
    assert kelce["analysis"]["fair_source"] == "single book:draftkings"


def test_props_different_line_is_a_different_row():
    res = analyse_event_markets(demo.PROPS_FIXTURE["evt_kc_buf"], BOOKS)
    mahomes = [p for p in res["props"] if p["player"] == "Patrick Mahomes"]
    assert sorted(p["point"] for p in mahomes) == [259.5, 262.5]
    alt = next(p for p in mahomes if p["point"] == 259.5)
    assert alt["analysis"]["fair_source"] == "single book:caesars"


def test_one_sided_prop_has_note_and_no_price():
    res = analyse_event_markets(demo.PROPS_FIXTURE["evt_tor_mtl"], BOOKS)
    goal = next(p for p in res["props"] if p["market"] == "player_goal_scorer_anytime")
    assert goal["analysis"] is None and "one side" in goal["note"]
    assert res["summary"]["lines"] == 3 and res["summary"]["lines_priced"] == 2


def test_props_plays_sorted_and_above_edge():
    res = analyse_event_markets(demo.PROPS_FIXTURE["evt_bos_nyk"], BOOKS, min_edge=0.0)
    evs = [p["ev_per_dollar"] for p in res["plays"]]
    assert evs and evs == sorted(evs, reverse=True) and all(e > 0 for e in evs)
    top = res["plays"][0]
    assert (top["player"], top["side"], top["book"], top["price"]) == ("Jalen Brunson", "Over", "draftkings", 118)
    # the price beats fair, and fair is the de-vigged consensus, not the book
    assert top["fair_price"] < top["price"]
    assert analyse_event_markets(demo.PROPS_FIXTURE["evt_bos_nyk"], BOOKS, min_edge=0.5)["plays"] == []
    assert {p["book"] for p in res["plays"]} <= set(BOOKS)


def test_team_markets_pair_on_mirrored_points():
    blob = {"home_team": "Home", "away_team": "Away", "bookmakers": [
        {"key": bk, "markets": [{"key": "alternate_spreads", "outcomes": [
            {"name": "Home", "price": -110, "point": -3.5},
            {"name": "Away", "price": -110, "point": 3.5},
            {"name": "Home", "price": -300, "point": 3.5},
            {"name": "Away", "price": 240, "point": -3.5},
        ]}, {"key": "h2h_h1", "outcomes": [
            {"name": "Home", "price": -150}, {"name": "Away", "price": 130},
        ]}]} for bk in ("fanduel", "draftkings", "betmgm")]}
    rows = collect_event_markets(blob)
    alts = [r for r in rows if r["market"] == "alternate_spreads"]
    assert sorted(r["labels"] for r in alts) == [["Home +3.5", "Away -3.5"], ["Home -3.5", "Away +3.5"]]
    res = analyse_event_markets(blob, BOOKS)
    h1 = next(p for p in res["props"] if p["market"] == "h2h_h1")
    assert h1["labels"] == ["Home", "Away"] and h1["analysis"]["book_count"] == 3
    alt = next(p for p in res["props"] if p["labels"] == ["Home -3.5", "Away +3.5"])
    assert alt["analysis"]["sides"][0]["point"] == -3.5


# ---------------- API, demo mode ----------------

@pytest.fixture
def demo_client(monkeypatch):
    monkeypatch.setattr(main, "DEMO_MODE", True)
    monkeypatch.setattr(main, "ENABLE_PROPS", False)
    monkeypatch.delenv("ODDS_API_KEY", raising=False)
    return TestClient(main.app)


@pytest.fixture
def live_client(monkeypatch):
    monkeypatch.setattr(main, "DEMO_MODE", False)
    monkeypatch.setattr(main, "ENABLE_PROPS", False)
    monkeypatch.setenv("ODDS_API_KEY", "test-key")
    monkeypatch.setattr(main, "_client", None)
    return TestClient(main.app)


@pytest.mark.parametrize("league", ["mlb", "nfl", "ncaaf", "nba", "nhl"])
def test_demo_board_route_every_league(demo_client, league):
    """Every demo league renders; the synthetic ones each carry one outlier play."""
    r = demo_client.get(f"/api/board/{league}")
    assert r.status_code == 200, r.text
    j = r.json()
    assert j["league"] == league and j["demo_mode"] is True
    assert j["summary"]["games_with_odds"] >= 2
    assert all(g["home"]["name"] for g in j["games"])
    label = j["sources"]["odds"]
    assert ("frozen Sep 15 2026" in label) if league == "mlb" else ("synthetic" in label)
    if league != "mlb":
        assert j["summary"]["plays_found"] >= 1


def test_demo_board_mlb_contract_unchanged(demo_client):
    j = demo_client.get("/api/board/mlb?stake=25&method=multiplicative").json()
    for k in ("generated_at", "your_books", "sharp_book", "devig_method", "stake",
              "games", "plays", "summary", "league", "usage", "sources", "demo_mode"):
        assert k in j
    assert j["stake"] == 25 and j["devig_method"] == "multiplicative"
    assert j["summary"]["games_total"] == 4 and j["summary"]["games_with_odds"] == 3
    for k in ("game", "market", "side", "point", "book", "price", "fair_price",
              "ev_per_dollar", "ev_dollars", "kelly", "quarter_kelly"):
        assert k in j["plays"][0]


def test_board_query_alias_and_market_filter(demo_client):
    j = demo_client.get("/api/board?sport=nfl&markets=spreads").json()
    assert j["league"] == "nfl" and j["markets"] == ["spreads"]
    assert all(p["market"] == "spreads" for p in j["plays"])
    assert all(g["markets"]["h2h"] is None for g in j["games"])


@pytest.mark.parametrize("url,code", [
    ("/api/board/curling", 400),
    ("/api/board/nfl?markets=player_pass_yds", 400),
    ("/api/board/nfl?method=vibes", 400),
    ("/api/props/nfl/evt_nope", 404),
    ("/api/props/nfl/evt_kc_buf?markets=a,b,c,d,e,f", 400),
])
def test_bad_requests_are_4xx(demo_client, url, code):
    assert demo_client.get(url).status_code == code


def test_demo_board_league_without_fixture(demo_client):
    j = demo_client.get("/api/board/wnba").json()
    assert j["games"] == [] and "No demo fixture" in j["note"]


def test_sports_catalog(demo_client):
    j = demo_client.get("/api/sports").json()
    by = {s["league"]: s for s in j["sports"]}
    assert {"mlb", "nfl", "ncaaf", "nba", "nhl"} <= {k for k, v in by.items() if v["demo_fixture"]}
    assert by["nfl"]["odds_api_key"] == "americanfootball_nfl"
    assert set(by["nba"]["default_prop_markets"]) <= set(by["nba"]["prop_markets"])


def test_demo_schedule_standings_injuries(demo_client):
    s = demo_client.get("/api/schedule/nfl").json()
    assert s["count"] == 4 and "synthetic" in s["source"]
    st = demo_client.get("/api/standings/nfl").json()["standings"]
    assert any(r["abbr"] == "BUF" and r["wins"] == 3 for r in st)
    inj = demo_client.get("/api/injuries/nfl").json()
    assert inj["count"] == 2
    assert demo_client.get("/api/injuries/nhl").json()["count"] == 0


def test_demo_events_then_props(demo_client):
    ev = demo_client.get("/api/events/nfl").json()["events"]
    with_props = [e["odds_id"] for e in ev if e["demo_props"]]
    assert with_props == ["evt_kc_buf"]
    j = demo_client.get(f"/api/props/nfl/{with_props[0]}").json()
    assert j["markets"] == ["player_pass_yds", "player_anytime_td"]
    assert {p["market"] for p in j["props"]} == set(j["markets"])
    assert j["estimated_cost"] == 0 and j["usage"] == {"demo": True}
    assert "synthetic" in j["source"]


def test_props_query_alias_and_generic_markets(demo_client):
    a = demo_client.get("/api/props?sport=nba&event_id=evt_bos_nyk&markets=player_rebounds").json()
    assert a["count"] == 1
    b = demo_client.get("/api/markets/nhl/evt_tor_mtl?markets=player_shots_on_goal").json()
    assert b["props"][0]["player"] == "Auston Matthews"
    assert demo_client.get("/api/markets/nhl/evt_tor_mtl").status_code == 422


# ---------------- API, live mode: credits ----------------

def test_live_props_refused_unless_enabled(live_client):
    r = live_client.get("/api/props/nfl/evt1")
    assert r.status_code == 403 and "ENABLE_PROPS" in r.json()["detail"]
    assert main._client is None      # refused before a client even existed


def test_live_props_dry_run_prices_without_calling(live_client):
    j = live_client.get("/api/props/nfl/evt1?dry_run=true&markets=a,b,c").json()
    assert j["dry_run"] is True and j["estimated_cost"] == 3
    assert main._client is None


class _FakeClient:
    def __init__(self):
        self.calls = []
        self.usage = odds_api.Usage()

    def event_odds(self, league, event_id, markets, bookmakers=None):
        self.calls.append((league, event_id, markets, bookmakers))
        return demo.demo_event_markets("evt_kc_buf", markets)


def test_live_props_when_enabled(live_client, monkeypatch):
    fake = _FakeClient()
    monkeypatch.setattr(main, "ENABLE_PROPS", True)
    monkeypatch.setattr(main, "_client", fake)
    j = live_client.get("/api/props/nfl/evt_kc_buf?markets=player_rush_yds").json()
    # blank books -> region pricing, so the consensus sees every US book
    assert fake.calls == [("nfl", "evt_kc_buf", ("player_rush_yds",), None)]
    assert j["source"] == "The Odds API" and j["estimated_cost"] == 1
    assert j["props"][0]["analysis"]["book_count"] == 4


class _Resp:
    status_code = 200
    text = ""

    def __init__(self):
        self.headers = {"x-requests-remaining": "497", "x-requests-used": "3",
                        "x-requests-last": "3"}

    def raise_for_status(self):
        pass

    def json(self):
        return []


class _HTTP:
    hits = 0

    def __init__(self, *a, **k):
        pass

    def __enter__(self):
        return self

    def __exit__(self, *a):
        return False

    def get(self, *a, **k):
        _HTTP.hits += 1
        return _Resp()


def test_client_cache_saves_credits(monkeypatch):
    monkeypatch.setattr(odds_api.httpx, "Client", _HTTP)
    _HTTP.hits = 0
    c = OddsAPI("k", cache_ttl=60)
    c.odds("nfl")
    c.odds("nfl")
    c.odds("nba")
    assert _HTTP.hits == 2
    u = c.usage.to_dict()
    assert u["cache_hits"] == 1 and u["credits_remaining"] == 497
    with pytest.raises(ValueError):
        c.event_odds("curling", "e", ("player_points",))
