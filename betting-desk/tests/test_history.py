"""
Line-movement tracking: the snapshot store, the history builder, and the
/api/history route in demo and (faked) live mode. Offline.
"""
import pytest
from fastapi.testclient import TestClient

from app import demo, main
from app.services import history
from app.services.board import build_board
from app.sources import odds_api
from app.sources.odds_api import normalise_game_lines


def _row(price, point=-3.0, side="H", book="fd", market="spreads", **kw):
    return {"event_id": "e1", "home": "H", "away": "A", "start_utc": None,
            "market": market, "book": book, "side": side, "point": point,
            "price": price, **kw}


# ---------------- store ----------------

def test_store_writes_only_changes(tmp_path):
    s = history.HistoryStore(str(tmp_path / "h.sqlite3"))
    assert s.rows("e1") == []                         # no file yet is not an error
    assert s.record("nfl", [_row(-110)], "t1") == 1
    assert s.record("nfl", [_row(-110)], "t2") == 0   # unchanged
    assert s.record("nfl", [_row(-115)], "t3") == 1   # price moved
    assert s.record("nfl", [_row(-115, point=-3.5)], "t4") == 1
    assert s.record("nfl", [_row(-115, point=-3.0)], "t5") == 1   # back to -3: still a move
    assert [r["ts"] for r in s.rows("e1")] == ["t1", "t3", "t4", "t5"]


def test_store_keys_prop_ladders_on_point(tmp_path):
    s = history.HistoryStore(str(tmp_path / "h.sqlite3"))
    ladder = [_row(-110, point=p, side="Over", market="player_points", description="P")
              for p in (20.5, 22.5, 24.5)]
    assert s.record("nba", ladder, "t1") == 3
    assert s.record("nba", ladder, "t2") == 0         # alternates don't thrash each other


# ---------------- builder ----------------

def test_demo_history_ends_at_the_board_fair_price():
    """The last fair point in the synthetic history is the board's own fair price."""
    rows = demo.demo_history_rows("evt_kc_buf")
    d = demo.DEMO_LEAGUES["nfl"]
    board = build_board(d["espn"], normalise_game_lines(d["odds"]), your_books=())
    game = next(g for g in board["games"] if g["odds_id"] == "evt_kc_buf")
    for market in ("h2h", "spreads", "totals"):
        h = history.build_history(rows, market)
        assert h["series"] and h["fair"], market
        last = h["fair"][-1]
        want = [s["fair_prob"] for s in game["markets"][market]["sides"]]
        assert [s["fair_prob"] for s in last["sides"]] == pytest.approx(want, abs=1e-5)
        if market != "h2h":
            assert last["line"] == game["markets"][market]["sides"][0]["point"]


def test_history_series_show_the_move():
    h = history.build_history(demo.demo_history_rows("evt_kc_buf"), "spreads", book="fanduel")
    assert {s["book"] for s in h["series"]} == {"fanduel"}
    # a new number is a new series: the spread moved through several points
    home = [s for s in h["series"] if s["side"] == "Buffalo Bills"]
    assert len({s["point"] for s in home}) >= 2
    lines = [p["line"] for p in h["fair"]]
    assert lines[0] != lines[-1]


def test_history_for_a_prop_has_no_fair_series():
    rows = [_row(-110, point=24.5, side="Over", market="player_points", description="P", ts="t1"),
            _row(-110, point=24.5, side="Under", market="player_points", description="P", ts="t1")]
    h = history.build_history(rows, "player_points", player="p")
    assert len(h["series"]) == 2 and h["fair"] == [] and "fair_note" in h


# ---------------- routes ----------------

@pytest.fixture
def demo_client(monkeypatch):
    monkeypatch.setattr(main, "DEMO_MODE", True)
    return TestClient(main.app)


def test_demo_history_route(demo_client):
    r = demo_client.get("/api/history/nfl/evt_kc_buf?market=totals")
    assert r.status_code == 200, r.text
    j = r.json()
    assert j["demo_mode"] is True and "synthetic" in j["source"]
    assert j["snapshots"] > 0 and j["fair"] and j["recording"] is False
    assert j["event"]["home"] == "Buffalo Bills"


@pytest.mark.parametrize("league", ["mlb", "nfl", "ncaaf", "nba", "nhl"])
def test_every_demo_game_has_history(demo_client, league):
    for ev in demo_client.get(f"/api/events/{league}").json()["events"]:
        j = demo_client.get(f"/api/history/{league}/{ev['odds_id']}?market=h2h").json()
        assert j["snapshots"] > 0 and j["fair"], (league, ev["odds_id"])


def test_history_route_errors(demo_client):
    assert demo_client.get("/api/history/nfl/nope").status_code == 404
    assert demo_client.get("/api/history/curling/evt_kc_buf").status_code == 400
    assert demo_client.get("/api/history/nfl/evt_kc_buf?method=vibes").status_code == 400
    assert demo_client.get("/api/history/nfl/bad.id").status_code == 400


class _FakeBoardClient:
    def __init__(self, events):
        self.events_ = events
        self.usage = odds_api.Usage()

    def odds(self, league, markets, bookmakers=None):
        return self.events_


@pytest.fixture
def live(monkeypatch):
    monkeypatch.setattr(main, "DEMO_MODE", False)
    monkeypatch.setenv("ODDS_API_KEY", "test-key")
    monkeypatch.setattr(main, "RECORD_HISTORY", True)
    monkeypatch.setattr(main.espn, "scoreboard", lambda *a, **k: [])
    return TestClient(main.app)


def test_live_board_records_and_history_reads_it_back(live, monkeypatch):
    import copy
    events = copy.deepcopy(demo.NFL_ODDS)
    fake = _FakeBoardClient(events)
    monkeypatch.setattr(main, "_client", fake)

    first = live.get("/api/board/nfl").json()
    assert first["history"]["recorded"] > 0
    assert live.get("/api/board/nfl").json()["history"]["recorded"] == 0   # nothing moved

    # the market moves at one book
    bm = events[0]["bookmakers"][0]
    spread = next(m for m in bm["markets"] if m["key"] == "spreads")
    spread["outcomes"][0]["price"] -= 10
    assert live.get("/api/board/nfl").json()["history"]["recorded"] == 1

    eid = events[0]["id"]
    j = live.get(f"/api/history/nfl/{eid}?market=spreads&book={bm['key']}").json()
    assert j["source"].startswith("local snapshots")
    moved = [s for s in j["series"] if s["moves"] == 1]
    assert len(moved) == 1 and moved[0]["current"] == moved[0]["open"] - 10
    assert live.get(f"/api/history/nfl/{eid}?market=spreads").json()["fair"]


def test_history_write_failure_does_not_break_the_board(live, monkeypatch, tmp_path):
    blocker = tmp_path / "file"
    blocker.write_text("")
    monkeypatch.setattr(main, "HISTORY_DB", str(blocker / "sub" / "h.sqlite3"))
    monkeypatch.setattr(main, "_history", None)
    monkeypatch.setattr(main, "_client", _FakeBoardClient(demo.NFL_ODDS))
    r = live.get("/api/board/nfl")
    assert r.status_code == 200
    assert r.json()["history"]["error"]


def test_empty_live_history_explains_itself(live):
    j = live.get("/api/history/nfl/abc123?market=h2h").json()
    assert j["snapshots"] == 0 and "No snapshots" in j["note"]
