"""
Alerts: edge crossings, sharp moves, de-duplication, skipped bad boards,
persistence, and the routes. Offline.

Run: python3 -m pytest tests/test_alerts.py
"""
import copy

import pytest
from fastapi.testclient import TestClient

from app import demo, main
from app.alerts import AlertStore


def _play(ev, book="fanduel", side="Bills", price=120):
    return {"game": "Bills at Chiefs", "odds_id": "e1", "market": "h2h", "side": side,
            "point": None, "book": book, "price": price, "fair_price": 110,
            "fair_source": "consensus:3 books", "ev_per_dollar": ev, "start_utc": "2026-10-06T00:20:00Z"}


def _board(plays=(), fair=0.5, point=None, market="h2h", **kw):
    sides = [{"label": "Bills", "fair_prob": fair, "fair_price": 100, "point": point},
             {"label": "Chiefs", "fair_prob": 1 - fair, "fair_price": -100,
              "point": None if point is None else -point}]
    g = {"name": "Bills at Chiefs", "odds_id": "e1", "has_odds": True, "state": "pre",
         "markets": {market: {"sides": sides}}}
    return {"games": [g], "plays": list(plays), "errors": [], "stale_odds": False, **kw}


@pytest.fixture
def store():
    s = AlertStore(None)
    s.config.update(min_edge=0.03, move_prob=0.03, move_points=1.0, enabled=True)
    return s


def test_edge_alert_fires_once_on_crossing(store):
    assert store.evaluate("nfl", _board([_play(0.02)])) == []          # below threshold
    new = store.evaluate("nfl", _board([_play(0.045)]))
    assert len(new) == 1 and new[0]["kind"] == "edge" and new[0]["id"] == 1
    assert "+4.5%" in new[0]["title"] and "fanduel" in new[0]["title"]
    assert store.evaluate("nfl", _board([_play(0.05)])) == []          # still above: no repeat
    store.evaluate("nfl", _board([_play(0.01)]))                       # drops below
    assert len(store.evaluate("nfl", _board([_play(0.04)]))) == 1      # crosses again


def test_first_board_alerts_existing_edges_and_each_book_separately(store):
    new = store.evaluate("nfl", _board([_play(0.05), _play(0.06, book="draftkings")]))
    assert {a["book"] for a in new} == {"fanduel", "draftkings"}


def test_threshold_is_configurable_and_clamped(store):
    store.set_config({"min_edge": 0.10, "move_prob": -1, "bogus": 5})
    assert store.config["min_edge"] == 0.10 and store.config["move_prob"] == 0.005
    assert store.evaluate("nfl", _board([_play(0.05)])) == []


def test_fair_probability_move_alerts_the_side_that_shortened(store):
    store.evaluate("nfl", _board(fair=0.50))
    assert store.evaluate("nfl", _board(fair=0.52)) == []              # 2 points: noise
    new = store.evaluate("nfl", _board(fair=0.56))
    assert len(new) == 1 and new[0]["kind"] == "move"
    assert new[0]["side"] == "Bills" and new[0]["to_prob"] == 0.56
    new = store.evaluate("nfl", _board(fair=0.40))
    assert new[0]["side"] == "Chiefs"


def test_line_move_on_spreads(store):
    store.evaluate("nfl", _board(point=3.0, market="spreads"))
    assert store.evaluate("nfl", _board(point=3.5, market="spreads")) == []   # half a point
    new = store.evaluate("nfl", _board(point=4.5, market="spreads", fair=0.9))
    assert len(new) == 1 and "3.5 → +4.5" in new[0]["title"]


def test_failed_or_stale_boards_do_not_reset_state(store):
    store.evaluate("nfl", _board([_play(0.05)]))
    assert store.evaluate("nfl", _board([], stale_odds=True)) == []
    assert store.evaluate("nfl", {**_board([]), "errors": [{"source": "odds"}]}) == []
    assert store.evaluate("nfl", {"games": [], "plays": []}) == []
    # the edge is still remembered, so recovery doesn't re-alert it
    assert store.evaluate("nfl", _board([_play(0.05)])) == []


def test_disabled_means_silent(store):
    store.set_config({"enabled": False})
    assert store.evaluate("nfl", _board([_play(0.5)])) == []


def test_finished_games_never_alert(store):
    b = _board([_play(0.2)])
    b["games"][0]["state"] = "post"
    assert store.evaluate("nfl", b) == []


def test_history_unread_and_persistence(tmp_path):
    path = str(tmp_path / "a.json")
    s = AlertStore(path)
    s.set_config({"min_edge": 0.01})
    s.evaluate("nfl", _board([_play(0.05)]))
    s.evaluate("nfl", _board([_play(0.05, book="draftkings")]))
    assert s.unread() == 2 and [a["id"] for a in s.list()] == [2, 1]
    assert [a["id"] for a in s.list(after=1)] == [2]
    s.mark_read(1)
    s2 = AlertStore(path)                                             # restart
    assert s2.config["min_edge"] == 0.01 and s2.unread() == 1 and s2.seq == 2
    s2.clear()
    assert s2.list() == [] and s2.unread() == 0


def test_corrupt_file_is_ignored(tmp_path):
    p = tmp_path / "a.json"
    p.write_text("{nope")
    assert AlertStore(str(p)).list() == []


# ---------------- through the routes ----------------

@pytest.fixture
def client(monkeypatch):
    monkeypatch.setattr(main, "DEMO_MODE", True)
    return TestClient(main.app)


def test_board_attaches_new_alerts_and_history_lists_them(client):
    assert client.post("/api/alerts/config", json={"min_edge": 0.01}).json()["config"]["min_edge"] == 0.01
    b = client.get("/api/board/nba").json()                 # demo NBA has a +4.7% play
    assert b["alerts"] and b["alerts"][0]["kind"] == "edge" and b["alerts"][0]["origin"] == "manual"
    assert client.get("/api/board/nba").json()["alerts"] == []
    j = client.get("/api/alerts").json()
    assert j["unread"] == len(j["alerts"]) >= 1 and j["persisted"] is False   # demo: memory only
    assert client.post("/api/alerts/read", json={}).json()["unread"] == 0
    assert client.get(f"/api/alerts?after={j['latest_id']}").json()["alerts"] == []
    assert client.delete("/api/alerts").json()["cleared"]
    assert client.get("/api/alerts").json()["alerts"] == []


def test_alert_config_validation(client):
    assert client.post("/api/alerts/config", json={"min_edge": 2}).status_code == 422
    assert client.post("/api/alerts/config", json={"move_prob": 0}).status_code == 422
    assert client.get("/api/alerts?limit=0").status_code == 422


class _FakeClient:
    def __init__(self, events):
        from app.sources import odds_api
        self.events_, self.usage, self.calls = events, odds_api.Usage(), 0
        self.last_stale, self.last_fetched_at = False, None

    def odds(self, league, markets, bookmakers=None, cached_only=False):
        self.calls += 0 if cached_only else 1
        return self.events_


def test_live_move_alert_and_cached_only_is_free_and_silent(monkeypatch):
    monkeypatch.setattr(main, "DEMO_MODE", False)
    monkeypatch.setenv("ODDS_API_KEY", "k")
    monkeypatch.setattr(main.espn, "scoreboard", lambda *a, **k: [])
    events = copy.deepcopy(demo.NFL_ODDS)
    fake = _FakeClient(events)
    monkeypatch.setattr(main, "_client", fake)
    c = TestClient(main.app)
    c.post("/api/alerts/config", json={"min_edge": 0.5, "move_prob": 0.02})
    assert c.get("/api/board/nfl").json()["alerts"] == []
    # every book shortens the home side on the first game's moneyline
    for bm in events[0]["bookmakers"]:
        for m in bm["markets"]:
            if m["key"] == "h2h":
                for o in m["outcomes"]:
                    o["price"] = -200 if o["name"] == events[0]["home_team"] else 170
    b = c.get("/api/board/nfl?cached_only=true").json()
    assert b["cached_only"] is True and b["alerts"] == [] and fake.calls == 1
    b = c.get("/api/board/nfl").json()
    moves = [a for a in b["alerts"] if a["kind"] == "move" and a["market"] == "h2h"]
    assert moves and moves[0]["side"] == events[0]["home_team"]
    assert c.get("/api/alerts").json()["persisted"] is True


def test_cached_only_never_spends_on_the_real_client(monkeypatch):
    from app.sources.odds_api import OddsAPI, OddsAPIError
    c = OddsAPI("k", cache_ttl=0)
    monkeypatch.setattr(c, "_fetch", lambda *a: pytest.fail("network touched"))
    with pytest.raises(OddsAPIError) as e:
        c.odds("nfl", cached_only=True)
    assert e.value.kind == "cache_miss"
    monkeypatch.setattr(c, "_fetch", lambda *a: [{"id": "x"}])
    assert c.odds("nfl") == [{"id": "x"}]
    monkeypatch.setattr(c, "_fetch", lambda *a: pytest.fail("network touched"))
    assert c.odds("nfl", cached_only=True) == [{"id": "x"}]   # past TTL, still served
    assert c.last_fetched_at is not None
