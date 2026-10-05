"""
Credit-aware auto-refresh.

Off by default. With AUTO_REFRESH=1 a background thread snapshots the board
for each league in AUTO_REFRESH_LEAGUES on a cadence that tightens as the
next game approaches, and stops spending for the day when it reaches a hard
credit budget.

Why it is shaped like this:

- **Cadence follows the clock to first pitch / kickoff.** Lines move most in
  the last hours, so that's where snapshots are worth a credit. Default tiers
  (AUTO_REFRESH_CADENCE): more than 24h out every 6h, 6-24h every 2h,
  2-6h every 45 min, 30 min-2h every 15 min, under 30 min every 10 min.
  With no game in the next AUTO_REFRESH_HORIZON_DAYS it spends nothing.
- **The daily budget is hard.** A run whose estimated cost would take the
  day past AUTO_REFRESH_DAILY_CREDITS is skipped, not trimmed. The estimate
  is markets x regions (the most a call can cost); the actual charge from
  the API's headers is what gets booked, so cache hits book 0.
- **Far-from-game runs can't starve the close.** Runs more than
  AUTO_REFRESH_FAR_HOURS before the next start may use only
  AUTO_REFRESH_FAR_SHARE of the day's budget; the rest is kept for the
  hours before games, which is when the snapshot doubles as a closing line.
- **The month has a floor.** When the API reports AUTO_REFRESH_RESERVE
  credits or fewer left, the scheduler stops so manual refreshes still work.
- **Schedules are free.** Start times come from The Odds API's /events
  endpoint, which costs nothing.

The day resets at 00:00 UTC. Spend and the run log survive restarts
(data/scheduler.json). Run one server worker: each worker would start its
own scheduler.
"""
from __future__ import annotations

import datetime as dt
import json
import logging
import os
import re
import threading
import time
from typing import Any, Callable

log = logging.getLogger("betting_desk.scheduler")

DEFAULT_PATH = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "data", "scheduler.json")
DEFAULT_CADENCE = "24h=360,6h=120,2h=45,30m=15,0=10"
EVENTS_TTL = 1800          # seconds between free /events lookups per league
TICK_SECONDS = 30
MAX_RUNS = 100


def parse_cadence(spec: str) -> list[tuple[int, int]]:
    """
    "24h=360,2h=45,0=10" -> [(86400, 360), (7200, 45), (0, 10)]: when the next
    start is more than <threshold> away, refresh every <minutes>. Sorted
    furthest-first; a 0 tier is added if missing so there is always a match.
    """
    tiers: list[tuple[int, int]] = []
    for part in (spec or "").split(","):
        part = part.strip()
        if not part:
            continue
        m = re.fullmatch(r"(\d+(?:\.\d+)?)\s*([dhm]?)\s*=\s*(\d+)", part)
        if not m:
            raise ValueError(f"bad cadence tier '{part}': use e.g. 6h=120 or 30m=15")
        n, unit, minutes = float(m.group(1)), m.group(2) or "m", int(m.group(3))
        if minutes < 1:
            raise ValueError("cadence minutes must be at least 1")
        secs = int(n * {"d": 86400, "h": 3600, "m": 60}[unit])
        tiers.append((secs, minutes))
    if not tiers:
        raise ValueError("empty cadence")
    tiers.sort(key=lambda t: -t[0])
    if tiers[-1][0] != 0:
        tiers.append((0, tiers[-1][1]))
    return tiers


def _parse_utc(v: Any) -> dt.datetime | None:
    if isinstance(v, dt.datetime):
        return v if v.tzinfo else v.replace(tzinfo=dt.timezone.utc)
    if not isinstance(v, str) or not v:
        return None
    try:
        t = dt.datetime.fromisoformat(v.replace("Z", "+00:00"))
    except ValueError:
        return None
    return t if t.tzinfo else t.replace(tzinfo=dt.timezone.utc)


def _iso(ts: float | None) -> str | None:
    return None if ts is None else dt.datetime.fromtimestamp(ts, dt.timezone.utc).isoformat()


def _day(ts: float) -> str:
    return dt.datetime.fromtimestamp(ts, dt.timezone.utc).date().isoformat()


class Scheduler:
    """
    `fetch(league)` builds and stores one board and returns
    {"cost": credits actually spent or None, "board": {...}, "alerts": [...]}.
    `events(league)` returns start times (ISO strings or datetimes); it must
    be free. `credits_left()` returns the API's remaining credits or None.
    `clock` is injectable so tests can walk through a day in milliseconds.
    """

    def __init__(
        self,
        fetch: Callable[[str], dict],
        events: Callable[[str], list],
        *,
        leagues: tuple[str, ...],
        daily_budget: int,
        cost_per_run: int,
        enabled: bool = False,
        reserve: int = 100,
        cadence: str | list[tuple[int, int]] = DEFAULT_CADENCE,
        far_hours: float = 3.0,
        far_share: float = 0.5,
        horizon_days: float = 7.0,
        credits_left: Callable[[], int | None] = lambda: None,
        state_path: str | None = DEFAULT_PATH,
        clock: Callable[[], float] = time.time,
    ):
        self.fetch, self.events, self.credits_left, self.clock = fetch, events, credits_left, clock
        self.leagues = tuple(leagues)
        self.daily_budget = max(0, int(daily_budget))
        self.cost_per_run = max(0, int(cost_per_run))
        self.enabled = enabled
        self.paused = False
        self.reserve = max(0, int(reserve))
        self.tiers = parse_cadence(cadence) if isinstance(cadence, str) else list(cadence)
        self.far_seconds = far_hours * 3600
        self.far_share = min(1.0, max(0.0, far_share))
        self.horizon = horizon_days * 86400
        self.state_path = state_path
        self._lock = threading.Lock()
        self._run_lock = threading.Lock()
        self._thread: threading.Thread | None = None
        self._stop = threading.Event()
        self.day = _day(self.clock())
        self.spent = 0
        self.runs: list[dict] = []
        # league -> {"last_run": ts, "starts": [ts], "events_at": ts, "skip": str|None, "error": str|None}
        self.leagues_state: dict[str, dict] = {lg: {} for lg in self.leagues}
        self._load()

    # ---------- persistence ----------

    def _load(self) -> None:
        if not self.state_path or not os.path.exists(self.state_path):
            return
        try:
            with open(self.state_path) as f:
                blob = json.load(f)
        except (OSError, ValueError):
            return
        if not isinstance(blob, dict):
            return
        if blob.get("day") == self.day:
            self.spent = int(blob.get("spent") or 0)
        self.runs = [r for r in blob.get("runs", []) if isinstance(r, dict)][-MAX_RUNS:]
        for lg, last in (blob.get("last_run") or {}).items():
            if lg in self.leagues_state and isinstance(last, (int, float)):
                self.leagues_state[lg]["last_run"] = float(last)

    def _save(self) -> None:
        if not self.state_path:
            return
        try:
            os.makedirs(os.path.dirname(self.state_path) or ".", exist_ok=True)
            tmp = self.state_path + ".tmp"
            with open(tmp, "w") as f:
                json.dump({"day": self.day, "spent": self.spent, "runs": self.runs,
                           "last_run": {lg: s.get("last_run") for lg, s in self.leagues_state.items()
                                        if s.get("last_run")}}, f)
            os.replace(tmp, self.state_path)
        except OSError as e:
            log.warning("scheduler state not saved: %s", e)

    # ---------- budget ----------

    def _roll_day(self, now: float) -> None:
        d = _day(now)
        if d != self.day:
            self.day, self.spent = d, 0

    def budget(self, now: float | None = None) -> dict:
        now = self.clock() if now is None else now
        with self._lock:
            self._roll_day(now)
            spent = self.spent
        tomorrow = dt.datetime.fromisoformat(self.day).replace(tzinfo=dt.timezone.utc) + dt.timedelta(days=1)
        return {
            "daily": self.daily_budget,
            "spent_today": spent,
            "remaining_today": max(0, self.daily_budget - spent),
            "far_cap": int(self.daily_budget * self.far_share),
            "cost_per_run": self.cost_per_run,
            "reserve": self.reserve,
            "credits_left": self.credits_left(),
            "resets_at": tomorrow.isoformat(),
        }

    def can_spend(self, cost: int, far: bool, now: float | None = None) -> str | None:
        """None if a run costing `cost` is allowed now; otherwise the reason it isn't."""
        now = self.clock() if now is None else now
        with self._lock:
            self._roll_day(now)
            spent = self.spent
        if spent + cost > self.daily_budget:
            return f"daily budget reached ({spent}/{self.daily_budget} credits)"
        if far and spent + cost > self.daily_budget * self.far_share:
            return (f"saving the rest of today's budget for the hours before games "
                    f"({spent}/{int(self.daily_budget * self.far_share)} credits allowed this far out)")
        left = self.credits_left()
        if left is not None and left - cost < self.reserve:
            return f"monthly reserve: {left} credits left, floor is {self.reserve}"
        return None

    def book(self, cost: int, now: float | None = None) -> None:
        now = self.clock() if now is None else now
        with self._lock:
            self._roll_day(now)
            self.spent += max(0, int(cost))

    # ---------- cadence ----------

    def interval_for(self, seconds_to_start: float) -> int:
        for threshold, minutes in self.tiers:
            if seconds_to_start > threshold:
                return minutes
        return self.tiers[-1][1]

    def _starts(self, league: str, now: float) -> list[float]:
        st = self.leagues_state.setdefault(league, {})
        if now - st.get("events_at", -1e18) >= EVENTS_TTL:
            try:
                starts = [t.timestamp() for t in map(_parse_utc, self.events(league) or []) if t]
                st.update(starts=sorted(starts), events_at=now, error=None)
            except Exception as e:      # keep the last known schedule
                st.update(events_at=now, error=f"schedule lookup failed: {e}")
        return st.get("starts") or []

    def plan(self, league: str, now: float | None = None) -> dict:
        """When this league is next due, and why."""
        now = self.clock() if now is None else now
        upcoming = [t for t in self._starts(league, now) if t > now]
        st = self.leagues_state.setdefault(league, {})
        if not upcoming or upcoming[0] - now > self.horizon:
            return {"league": league, "next_start": _iso(upcoming[0]) if upcoming else None,
                    "interval_min": None, "due_at": None, "far": True,
                    "idle": (f"next game is more than {self.horizon / 86400:g} days out"
                             if upcoming else "no upcoming games")}
        to_start = upcoming[0] - now
        interval = self.interval_for(to_start)
        last = st.get("last_run")
        due = now if last is None else last + interval * 60
        return {"league": league, "next_start": _iso(upcoming[0]), "interval_min": interval,
                "due_at": due, "far": to_start > self.far_seconds, "idle": None}

    # ---------- running ----------

    def tick(self, now: float | None = None) -> list[dict]:
        """One pass over the leagues. Returns what happened (for tests and logs)."""
        now = self.clock() if now is None else now
        done = []
        if not self.enabled or self.paused:
            return done
        for lg in self.leagues:
            p = self.plan(lg, now)
            if p["idle"] or p["due_at"] is None or p["due_at"] > now:
                self.leagues_state[lg]["skip"] = p["idle"]
                continue
            reason = self.can_spend(self.cost_per_run, p["far"], now)
            if reason:
                self.leagues_state[lg]["skip"] = reason
                done.append({"league": lg, "skipped": reason})
                continue
            done.append(self.run(lg, now, tier=p["interval_min"]))
        return done

    def run(self, league: str, now: float | None = None, tier: int | None = None,
            manual: bool = False) -> dict:
        """Snapshot one league now. Books the actual cost. Never raises."""
        now = self.clock() if now is None else now
        if manual:
            reason = self.can_spend(self.cost_per_run, False, now)
            if reason:
                return {"league": league, "skipped": reason}
        with self._run_lock:
            entry: dict[str, Any] = {"at": _iso(now), "league": league, "tier_min": tier,
                                     "manual": manual}
            try:
                res = self.fetch(league) or {}
                cost = res.get("cost")
                cost = self.cost_per_run if cost is None else int(cost)
                board = res.get("board") or {}
                entry.update(ok=True, cost=cost,
                             games=sum(1 for g in board.get("games") or [] if g.get("has_odds")),
                             plays=len(board.get("plays") or []),
                             alerts=len(res.get("alerts") or []),
                             errors=[e.get("message") for e in board.get("errors") or []][:3])
                starts = [t.timestamp() for t in (_parse_utc(g.get("start_utc"))
                                                  for g in board.get("games") or []) if t]
                if starts and not self.leagues_state.get(league, {}).get("starts"):
                    self.leagues_state.setdefault(league, {})["starts"] = sorted(starts)
            except Exception as e:
                # a failed call may still have been billed; book the estimate to stay safe
                cost = self.cost_per_run
                entry.update(ok=False, cost=cost, error=f"{type(e).__name__}: {e}")
                log.warning("scheduled refresh of %s failed: %s", league, e)
            self.book(cost, now)
            with self._lock:
                st = self.leagues_state.setdefault(league, {})
                st.update(last_run=now, skip=None)
                self.runs = (self.runs + [entry])[-MAX_RUNS:]
                self._save()
            return entry

    # ---------- thread ----------

    def start(self) -> bool:
        if not self.enabled or (self._thread and self._thread.is_alive()):
            return False
        self._stop.clear()
        self._thread = threading.Thread(target=self._loop, name="bd-scheduler", daemon=True)
        self._thread.start()
        return True

    def stop(self) -> None:
        self._stop.set()
        if self._thread:
            self._thread.join(timeout=5)

    def _loop(self) -> None:
        while not self._stop.is_set():
            try:
                self.tick()
            except Exception as e:      # never let the thread die quietly
                log.exception("scheduler tick failed: %s", e)
            self._stop.wait(TICK_SECONDS)

    # ---------- status ----------

    def status(self) -> dict:
        now = self.clock()
        leagues = []
        for lg in self.leagues:
            p = self.plan(lg, now) if self.enabled else {"league": lg}
            st = self.leagues_state.get(lg, {})
            leagues.append({
                "league": lg,
                "next_start": p.get("next_start"),
                "interval_min": p.get("interval_min"),
                "next_run_at": _iso(p.get("due_at")) if p.get("due_at") is not None else None,
                "last_run_at": _iso(st.get("last_run")),
                "waiting": st.get("skip") or p.get("idle"),
                "error": st.get("error"),
            })
        return {
            "enabled": self.enabled,
            "paused": self.paused,
            "running": bool(self._thread and self._thread.is_alive()),
            "budget": self.budget(now),
            "cadence": [{"more_than_min": t // 60, "every_min": m} for t, m in self.tiers],
            "far_hours": self.far_seconds / 3600,
            "leagues": leagues,
            "runs": list(reversed(self.runs[-20:])),
            "now": _iso(now),
        }
