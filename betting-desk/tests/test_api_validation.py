"""
Bad input gets a 4xx that says what's wrong, never a 500 or a wrong number.
Found in the pass-2 review of the routes. Offline.
"""
import pytest
from fastapi.testclient import TestClient

from app import main
from app.math_engine import parlay_decimal, parlay_report


@pytest.fixture
def client(monkeypatch):
    monkeypatch.setattr(main, "DEMO_MODE", True)
    return TestClient(main.app)


@pytest.mark.parametrize("q", ["american=0", "american=-50", "american=99",
                               "decimal=1", "decimal=0.5", "prob=0", "prob=1", "prob=1.5"])
def test_convert_rejects_impossible_odds(client, q):
    r = client.get(f"/api/convert?{q}")
    assert r.status_code == 400, (q, r.text)


def test_convert_still_converts(client):
    assert client.get("/api/convert?american=-110").json()["decimal"] == 1.9091
    assert client.get("/api/convert?prob=0.5").json()["american"] == 100


def test_parlay_rejects_impossible_leg_and_stake(client):
    leg = {"label": "x", "american": 50}
    assert client.post("/api/parlay", json={"legs": [leg]}).status_code == 422
    ok = {"label": "x", "american": -110}
    assert client.post("/api/parlay", json={"legs": [ok], "stake": 0}).status_code == 422


def test_parlay_with_a_zero_probability_leg_does_not_crash(client):
    r = client.post("/api/parlay", json={"legs": [
        {"label": "a", "american": 150, "fair_prob": 0.0},
        {"label": "b", "american": -110, "fair_prob": 0.5}]})
    assert r.status_code == 200, r.text
    j = r.json()
    assert j["fair_prob"] == 0 and j["total_hold"] is None and j["ev_dollars"] == -10.0


def test_parlay_ev_uses_exact_decimal_not_rounded_american():
    legs = [{"label": str(i), "american": -110, "fair_prob": 0.53} for i in range(6)]
    r = parlay_report(legs, stake=100)
    dec = parlay_decimal([-110] * 6)
    p = 0.53 ** 6
    assert r["ev_dollars"] == round(100 * (p * (dec - 1) - (1 - p)), 2)


def test_event_id_is_validated_before_any_url_is_built(client, monkeypatch):
    monkeypatch.setattr(main, "DEMO_MODE", False)
    monkeypatch.setattr(main, "ENABLE_PROPS", True)
    monkeypatch.setattr(main, "_client", None)
    r = client.get("/api/props", params={"sport": "nfl", "event_id": "../../sports"})
    assert r.status_code == 400
    assert main._client is None


def test_live_injuries_failure_degrades_to_empty(client, monkeypatch):
    # injuries are context only: a broken feed returns an empty list plus an
    # error note rather than an error page (see test_robustness.py)
    monkeypatch.setattr(main, "DEMO_MODE", False)

    def boom(league):
        raise ValueError("not json")
    monkeypatch.setattr(main.espn, "injuries", boom)
    r = client.get("/api/injuries/nfl")
    assert r.status_code == 200
    assert r.json()["injuries"] == [] and "not json" in r.json()["error"]
