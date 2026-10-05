"""
Auto-refresh scheduler: cadence tiers, the hard daily budget, the far-from-game
cap, the monthly reserve, day rollover, persistence, and the routes.

Offline. The clock and the fetch are fakes, so a whole day runs in milliseconds.

Run: python3 -m pytest tests/test_scheduler.py
"""
import datetime as dt

import pytest
from fastapi.testclient import TestClient

from app import main
from app.scheduler import Scheduler, parse_cadence

T0 = dt.datetime(2026, 10, 5, 12, 0, tzinfo=dt.timezone.utc).timestamp()
H = 3600


class Clock:
    def __init__(self, t=T0):
        self.t = t

    def __call__(self):
        return self.t


def make(clock, starts, *, budget=15, cost=3, fetch_cost=None, reserve=0, credits=None,
         far_share=0.5, state_path=None, fetch=None, events=None):
    calls = []

    def _fetch(lg):
        calls.append((lg, clock()))
        return {"cost": cost if fetch_cost is None else fetch_cost,
                "board": {"games": [{"has_odds": True}], "plays": []}, "alerts": []}

    s = Scheduler(fetch or _fetch,
                  events or (lambda lg: [dt.datetime.fromtimestamp(t, dt.timezone.utc).isoformat()
                                         for t in starts]),
                  leagues=("nfl",), daily_budget=budget, cost_per_run=cost, enabled=True,
                  reserve=reserve, far_share=far_share, credits_left=lambda: credits,
                  state_path=state_path, clock=clock)
    return s, calls


def test_parse_cadence_sorts_and_adds_a_zero_tier():
    assert parse_cadence("2h=45,24h=360") == [(86400, 360), (7200, 45), (0, 45)]
    assert parse_cadence("1d=600,30m=15,0=5")[-1] == (0, 5)
    with pytest.raises(ValueError):
        parse_cadence("soon=5")
    with pytest.raises(ValueError):
        parse_cadence("")


def test_cadence_tightens_near_game_time():
    s, _ = make(Clock(), [])
    assert s.interval_for(48 * H) == 360
    assert s.interval_for(10 * H) == 120
    assert s.interval_for(3 * H) == 45
    assert s.interval_for(1 * H) == 15
    assert s.interval_for(10 * 60) == 10


def test_first_tick_runs_then_waits_for_the_interval():
    clock = Clock()
    s, calls = make(clock, [T0 + 1 * H], budget=100)     # one hour out: every 15 min
    s.tick()
    assert len(calls) == 1 and s.spent == 3
    clock.t += 10 * 60
    assert s.tick() == [] and len(calls) == 1             # not due yet
    clock.t += 5 * 60
    s.tick()
    assert len(calls) == 2 and s.spent == 6


def test_idle_with_no_games_spends_nothing():
    clock = Clock()
    s, calls = make(clock, [T0 - H, T0 + 30 * 86400])     # started already / a month away
    assert s.tick() == [] and calls == []
    assert "more than 7 days" in s.status()["leagues"][0]["waiting"]


def test_hard_daily_budget_stops_runs_and_resets_at_midnight_utc():
    clock = Clock()
    s, calls = make(clock, [T0 + 110 * 60], budget=10, cost=3)   # every 15 min near the game
    for _ in range(6):
        s.tick()
        clock.t += 15 * 60
    assert len(calls) == 3 and s.spent == 9               # a 4th run would make 12 > 10
    skipped = s.tick()
    assert skipped and "daily budget" in skipped[0]["skipped"]
    assert s.budget()["remaining_today"] == 1

    # next UTC day: budget is back, cadence resumes for tomorrow's game
    clock.t = dt.datetime(2026, 10, 6, 0, 5, tzinfo=dt.timezone.utc).timestamp()
    s.events = lambda lg: [dt.datetime(2026, 10, 6, 1, 0, tzinfo=dt.timezone.utc).isoformat()]
    s.leagues_state["nfl"]["events_at"] = -1e18           # force a schedule re-read
    s.tick()
    assert len(calls) == 4 and s.spent == 3 and s.day == "2026-10-06"


def test_far_from_game_runs_only_use_their_share():
    clock = Clock(T0 - 11.5 * H)                         # 00:30 UTC: four 6h ticks, one day
    s, calls = make(clock, [clock.t + 30 * H], budget=12, cost=3, far_share=0.5)
    for _ in range(4):
        s.tick()
        clock.t += 6 * H
    # far cap is 6 credits: two runs, then it saves the rest for the close
    assert len(calls) == 2
    assert "saving the rest" in s.leagues_state["nfl"]["skip"]
    assert s.can_spend(3, far=False) is None              # near-game runs still allowed


def test_monthly_reserve_floor():
    s, calls = make(Clock(), [T0 + H], credits=101, reserve=100)
    out = s.tick()
    assert calls == [] and "monthly reserve" in out[0]["skipped"]


def test_actual_cost_is_booked_not_the_estimate():
    s, _ = make(Clock(), [T0 + H], cost=3, fetch_cost=0)   # a cache hit costs nothing
    s.tick()
    assert s.spent == 0 and s.runs[-1]["cost"] == 0


def test_failed_fetch_books_the_estimate_and_never_raises():
    def boom(lg):
        raise RuntimeError("feed down")
    s, _ = make(Clock(), [T0 + H], fetch=boom)
    out = s.tick()
    assert out[0]["ok"] is False and "feed down" in out[0]["error"]
    assert s.spent == 3


def test_no_games_at_all_is_idle():
    s, calls = make(Clock(), [])
    assert s.tick() == [] and s.status()["leagues"][0]["waiting"] == "no upcoming games"


def test_schedule_lookup_failure_is_reported_not_raised():
    def bad(lg):
        raise RuntimeError("no key")
    s, calls = make(Clock(), [], events=bad)
    assert s.tick() == [] and calls == []
    assert "no key" in s.status()["leagues"][0]["error"]


def test_spend_and_runs_survive_a_restart(tmp_path):
    path = str(tmp_path / "s.json")
    clock = Clock()
    s, _ = make(clock, [T0 + H], state_path=path)
    s.tick()
    s2, calls = make(clock, [T0 + H], state_path=path)
    assert s2.spent == 3 and len(s2.runs) == 1
    s2.tick()                                             # last run restored: not due yet
    assert calls == []


def test_disabled_or_paused_does_nothing():
    s, calls = make(Clock(), [T0 + H])
    s.paused = True
    assert s.tick() == [] and calls == []
    s.paused, s.enabled = False, False
    assert s.tick() == [] and calls == [] and s.start() is False


# ---------------- routes ----------------

@pytest.fixture
def demo(monkeypatch):
    monkeypatch.setattr(main, "DEMO_MODE", True)
    return TestClient(main.app)


def test_scheduler_route_reports_off_by_default(demo):
    j = demo.get("/api/scheduler").json()
    assert j["enabled"] is False and "AUTO_REFRESH=1" in j["note"]
    assert j["budget"]["daily"] == 15
    assert demo.post("/api/scheduler/run/nfl").status_code == 409


def test_scheduler_route_runs_through_the_budget(demo, monkeypatch):
    monkeypatch.setattr(main, "AUTO_REFRESH", True)
    monkeypatch.setattr(main, "AUTO_REFRESH_LEAGUES", ("nfl", "curling"))
    j = demo.get("/api/scheduler").json()
    assert j["enabled"] and [lg["league"] for lg in j["leagues"]] == ["nfl"]
    r = demo.post("/api/scheduler/run/nfl")
    assert r.status_code == 200 and r.json()["run"]["ok"] and r.json()["run"]["cost"] == 0
    assert demo.get("/api/scheduler").json()["runs"][0]["league"] == "nfl"
    assert demo.post("/api/scheduler/pause").json()["paused"] is True
    assert demo.post("/api/scheduler/resume").json()["paused"] is False
    assert demo.post("/api/scheduler/run/curling").status_code == 400


def test_scheduler_run_refused_when_budget_is_spent(demo, monkeypatch):
    monkeypatch.setattr(main, "AUTO_REFRESH", True)
    s = main.scheduler()
    s.cost_per_run, s.daily_budget = 3, 2
    r = demo.post("/api/scheduler/run/nfl")
    assert r.status_code == 429 and "daily budget" in r.json()["detail"]
