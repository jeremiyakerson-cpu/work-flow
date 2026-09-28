"""
Tracker results: grading plays against final scores, and /api/results
never 500ing when ESPN misbehaves. ESPN is mocked with respx.
"""
import datetime as dt
import os
import sys

import httpx
import respx
from fastapi.testclient import TestClient

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from app import main  # noqa: E402
from app.services import results as R  # noqa: E402
from app.sources import espn  # noqa: E402

ESPN_URL = f"{espn.BASE}/baseball/mlb/scoreboard"


def _final(hs, as_, state="post"):
    return {"state": state, "home": {"name": "Tampa Bay Rays", "score": hs},
            "away": {"name": "Athletics", "score": as_}}


def _play(**kw):
    p = {"id": "x", "league": "mlb", "market": "h2h", "side": "Tampa Bay Rays", "point": None}
    p.update(kw)
    return p


# ---------------- grade() ----------------

def test_moneyline_win_loss():
    assert R.grade(_play(), _final(5, 3))["result"] == "won"
    assert R.grade(_play(side="Athletics"), _final(5, 3))["result"] == "lost"


def test_moneyline_name_variants_match():
    # odds feed names vs ESPN names differ slightly; still the same team
    assert R.grade(_play(side="Tampa Bay Rays."), _final(5, 3))["result"] == "won"


def test_spread_win_loss_push():
    assert R.grade(_play(market="spreads", side="Athletics", point=1.5), _final(4, 3))["result"] == "won"
    assert R.grade(_play(market="spreads", side="Tampa Bay Rays", point=-1.5), _final(4, 3))["result"] == "lost"
    assert R.grade(_play(market="spreads", side="Tampa Bay Rays", point=-1.0), _final(4, 3))["result"] == "push"


def test_totals():
    assert R.grade(_play(market="totals", side="Over", point=8.5), _final(5, 4))["result"] == "won"
    assert R.grade(_play(market="totals", side="Under", point=8.5), _final(5, 4))["result"] == "lost"
    assert R.grade(_play(market="totals", side="Under", point=9), _final(5, 4))["result"] == "push"


def test_not_final_is_pending_or_live():
    assert R.grade(_play(), _final(None, None, "pre"))["status"] == "pending"
    g = R.grade(_play(), _final(2, 1, "in"))
    assert g["status"] == "live" and g["result"] is None


def test_props_and_unknown_teams_are_ungradeable():
    assert R.grade(_play(market="player_pass_yds", side="Josh Allen Over", point=254.5),
                   _final(5, 3))["result"] == "ungradeable"
    assert R.grade(_play(side="Boston Red Sox"), _final(5, 3))["result"] == "ungradeable"


def test_final_without_score():
    g = R.grade(_play(), _final(None, None))
    assert g["status"] == "final" and g["result"] is None


def test_missing_game():
    assert R.grade(_play(), None)["status"] == "unknown"


def test_espn_dates_cover_late_starts():
    # 01:38Z on the 16th is the evening of the 15th in the US
    assert R.espn_dates("2026-09-16T01:38:00Z") == [dt.date(2026, 9, 15), dt.date(2026, 9, 16)]
    assert R.espn_dates("2026-09-15T22:40:00Z") == [dt.date(2026, 9, 15)]
    assert R.espn_dates("garbage") == [] and R.espn_dates(None) == []


def test_demo_finals_are_stable_and_never_tied():
    p = _play(espn_id="401", home="Tampa Bay Rays", away="Athletics")
    a, b = R.demo_game(p), R.demo_game(p)
    assert a == b
    assert a["home"]["score"] != a["away"]["score"]


# ---------------- /api/results ----------------

ESPN_FINAL = {"events": [{
    "id": "401", "name": "Athletics at Tampa Bay Rays", "date": "2026-09-15T22:40Z",
    "status": {"type": {"state": "post", "detail": "Final", "completed": True}},
    "competitions": [{"competitors": [
        {"homeAway": "home", "score": "2", "team": {"abbreviation": "TB", "displayName": "Tampa Bay Rays"}},
        {"homeAway": "away", "score": "6", "team": {"abbreviation": "ATH", "displayName": "Athletics"}},
    ]}],
}]}

BODY = {"plays": [
    {"id": "ml", "league": "mlb", "market": "h2h", "side": "Athletics", "espn_id": "401",
     "start_utc": "2026-09-15T22:40:00Z"},
    {"id": "tot", "league": "mlb", "market": "totals", "side": "Under", "point": 8.5, "espn_id": "401",
     "start_utc": "2026-09-15T22:40:00Z"},
    {"id": "prop", "league": "mlb", "market": "batter_hits", "side": "X Over", "point": 0.5,
     "espn_id": "401", "start_utc": "2026-09-15T22:40:00Z"},
]}


def _client(monkeypatch, demo=False):
    monkeypatch.setattr(main, "DEMO_MODE", demo)
    return TestClient(main.app)


@respx.mock
def test_results_endpoint_grades_from_espn(monkeypatch):
    route = respx.get(ESPN_URL).mock(return_value=httpx.Response(200, json=ESPN_FINAL))
    r = _client(monkeypatch).post("/api/results", json=BODY)
    assert r.status_code == 200
    res = r.json()["results"]
    assert res["ml"]["result"] == "won"
    assert res["tot"]["result"] == "won"           # 6+2 = 8, under 8.5
    assert res["prop"]["result"] == "ungradeable"
    assert route.call_count == 1                   # one scoreboard call for the whole batch


@respx.mock
def test_results_endpoint_survives_espn_failure(monkeypatch):
    respx.get(ESPN_URL).mock(return_value=httpx.Response(503))
    r = _client(monkeypatch).post("/api/results", json=BODY)
    assert r.status_code == 200
    j = r.json()
    assert j["errors"] and j["errors"][0]["kind"] == "upstream"
    assert all(v["status"] == "unknown" for v in j["results"].values())


@respx.mock
def test_results_endpoint_timeout(monkeypatch):
    respx.get(ESPN_URL).mock(side_effect=httpx.ConnectTimeout("slow"))
    j = _client(monkeypatch).post("/api/results", json=BODY).json()
    assert j["errors"][0]["kind"] == "timeout"


def test_results_endpoint_demo_is_synthetic(monkeypatch):
    body = {"plays": [{"id": "a", "league": "mlb", "market": "h2h", "side": "Athletics",
                       "espn_id": "401", "home": "Tampa Bay Rays", "away": "Athletics"}]}
    j = _client(monkeypatch, demo=True).post("/api/results", json=body).json()
    assert j["demo_mode"] and j["results"]["a"]["synthetic"]
    assert j["results"]["a"]["result"] in ("won", "lost")


def test_results_endpoint_rejects_bad_body(monkeypatch):
    assert _client(monkeypatch).post("/api/results", json={"plays": [{"id": "a"}]}).status_code == 422
