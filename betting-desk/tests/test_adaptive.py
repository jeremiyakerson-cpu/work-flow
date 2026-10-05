"""
The adaptive model: book sharpness, weighted consensus, calibration and
suggested thresholds. Every number checked here is worked out by hand or
built into a fixture whose answer is known in advance. Offline.
"""
import datetime as dt
import math

import pytest
from fastapi.testclient import TestClient

from app import demo_adaptive, main
from app.math_engine import (
    brier_score, break_even_x, clv_vs_fair, consensus_fair_probs, devig,
    kl_divergence, linear_fit, log_loss, weighted_consensus_fair_probs, wilson_interval,
)
from app.services import history, sharpness
from app.services.board import analyse_market, fair_estimate
from app.services.calibration import (
    calibration_report, edge_bucket, normalise_play, reliability,
)
from app.services.thresholds import (
    ThresholdConfig, ThresholdStore, suggest_for, suggest_thresholds,
)

PAST = "2026-09-01T23:00:00+00:00"
NOW = dt.datetime(2026, 10, 5, tzinfo=dt.timezone.utc)


@pytest.fixture
def demo_client(monkeypatch):
    monkeypatch.setattr(main, "DEMO_MODE", True)
    return TestClient(main.app)


@pytest.fixture
def live_client(monkeypatch):
    monkeypatch.setattr(main, "DEMO_MODE", False)
    return TestClient(main.app)


# ---------------- math primitives ----------------

def test_weighted_consensus_equal_weights_is_the_plain_consensus():
    books = {"a": [-150, 130], "b": [-140, 120], "c": [-160, 135]}
    plain = consensus_fair_probs(books, "power")
    assert weighted_consensus_fair_probs(books, None) == pytest.approx(plain, abs=1e-12)
    assert weighted_consensus_fair_probs(books, {"a": 2, "b": 2, "c": 2}) == pytest.approx(plain, abs=1e-12)
    # a book missing from the weights counts as 1.0
    assert weighted_consensus_fair_probs(books, {"a": 1.0}) == pytest.approx(plain, abs=1e-12)


def test_weighted_consensus_is_the_weighted_mean_of_devigs():
    books = {"a": [-150, 130], "b": [-110, -110]}
    da, db = devig(books["a"]), devig(books["b"])
    got = weighted_consensus_fair_probs(books, {"a": 3.0, "b": 1.0})
    want = [(3 * da[i] + db[i]) / 4 for i in range(2)]
    s = sum(want)
    assert got == pytest.approx([w / s for w in want], abs=1e-12)
    assert sum(got) == pytest.approx(1.0)


def test_weighted_consensus_rejects_non_positive_weights_and_honours_exclude():
    books = {"a": [-150, 130], "b": [-110, -110]}
    with pytest.raises(ValueError):
        weighted_consensus_fair_probs(books, {"a": 0.0})
    assert weighted_consensus_fair_probs(books, {"a": 9.0}, exclude="a") == pytest.approx(devig(books["b"]))


def test_kl_divergence_known_values():
    want = 0.5 * math.log(0.5 / 0.6) + 0.5 * math.log(0.5 / 0.4)
    assert kl_divergence([0.5, 0.5], [0.6, 0.4]) == pytest.approx(want, abs=1e-12)
    assert kl_divergence([0.3, 0.7], [0.3, 0.7]) == 0.0
    assert kl_divergence([0.7, 0.3], [0.5, 0.5]) > 0
    with pytest.raises(ValueError):
        kl_divergence([0.5, 0.5], [1.0])


def test_brier_and_log_loss_known_values():
    assert brier_score([0.7, 0.4], [1, 0]) == pytest.approx((0.09 + 0.16) / 2)
    assert brier_score([0.5] * 4, [1, 0, 1, 0]) == pytest.approx(0.25)
    assert log_loss([0.7, 0.4], [1, 0]) == pytest.approx(-(math.log(0.7) + math.log(0.6)) / 2)
    assert math.isfinite(log_loss([1.0], [0]))          # clamped, not infinite
    with pytest.raises(ValueError):
        brier_score([], [])


def test_wilson_interval_known_values():
    lo, hi = wilson_interval(5, 10)
    assert (lo, hi) == pytest.approx((0.236590, 0.763410), abs=1e-5)
    lo, hi = wilson_interval(0, 10)
    assert lo == 0.0 and hi == pytest.approx(0.277533, abs=1e-5)
    assert wilson_interval(0, 0) is None


def test_clv_vs_fair():
    assert clv_vs_fair(100, 0.55) == pytest.approx(0.10)
    assert clv_vs_fair(-110, 0.5238095238) == pytest.approx(0.0, abs=1e-9)


def test_linear_fit_known_values():
    f = linear_fit([0, 1, 2, 3], [1, 3, 2, 5])
    assert f["b"] == pytest.approx(1.1)
    assert f["a"] == pytest.approx(1.1)
    assert f["se_b"] == pytest.approx(math.sqrt(1.35 / 5))
    assert f["se_a"] == pytest.approx(math.sqrt(1.35 * (0.25 + 2.25 / 5)))
    assert f["cov_ab"] == pytest.approx(-1.5 * 1.35 / 5)
    exact = linear_fit([0, 1, 2], [-1, 1, 3])
    assert (exact["a"], exact["b"], exact["se_b"]) == pytest.approx((-1, 2, 0))
    assert break_even_x(exact) == pytest.approx((0.5, 0.0))
    assert linear_fit([1, 1, 1], [1, 2, 3]) is None        # no spread in x
    assert linear_fit([1, 2], [1, 2]) is None              # too few points
    assert break_even_x(linear_fit([0, 1, 2], [3, 2, 1])) is None   # slope not positive


# ---------------- book sharpness ----------------

def _q(ts, book, a, b, market="h2h", pa=None, pb=None, start=PAST):
    sides = ("Over", "Under") if market == "totals" else ("H", "A")
    return [
        {"ts": ts, "league": "nfl", "event_id": "e1", "home": "H", "away": "A",
         "start_utc": start, "market": market, "book": book, "side": sides[0], "point": pa, "price": a},
        {"ts": ts, "league": "nfl", "event_id": "e1", "home": "H", "away": "A",
         "start_utc": start, "market": market, "book": book, "side": sides[1], "point": pb, "price": b},
    ]


def test_score_event_on_known_quotes():
    t1, t2 = "2026-09-01T10:00:00+00:00", "2026-09-01T20:00:00+00:00"
    rows = (_q(t1, "a", -110, -110) + _q(t1, "c", -150, 130)
            + _q(t2, "a", -110, -110) + _q(t2, "b", -110, -110) + _q(t2, "c", -110, -110))
    sc = sharpness.score_event(rows, "h2h", "power", now=NOW)
    # every leave-one-out close is 50/50
    assert sc["a"]["kl"] == pytest.approx(0.0, abs=1e-9) and sc["a"]["observations"] == 2
    assert sc["b"]["kl"] == pytest.approx(0.0, abs=1e-9) and sc["b"]["observations"] == 1
    c_t1 = devig([-150, 130])
    assert sc["c"]["kl"] == pytest.approx(kl_divergence([0.5, 0.5], c_t1) / 2)
    assert sc["c"]["bias"] == pytest.approx((c_t1[0] - 0.5) / 2)
    assert sc["c"]["abs_err"] == pytest.approx(abs(c_t1[0] - 0.5) / 2)


def test_score_event_ignores_unstarted_games_and_post_start_quotes():
    t1 = "2026-09-01T10:00:00+00:00"
    rows = _q(t1, "a", -110, -110) + _q(t1, "b", -110, -110) + _q(t1, "c", -120, 100)
    assert sharpness.score_event(rows, "h2h", now=dt.datetime(2026, 8, 1, tzinfo=dt.timezone.utc)) is None
    late = _q("2026-09-02T01:00:00+00:00", "c", -300, 250)     # after start: not a close
    sc = sharpness.score_event(rows + late, "h2h", now=NOW)
    assert sc["c"]["observations"] == 1
    assert sc["c"]["kl"] == pytest.approx(kl_divergence(consensus_fair_probs(
        {"a": [-110, -110], "b": [-110, -110]}), devig([-120, 100])))


def test_score_event_compares_books_on_the_closing_line_only():
    t = "2026-09-01T10:00:00+00:00"
    rows = (_q(t, "a", -110, -110, "spreads", -3.5, 3.5) + _q(t, "b", -110, -110, "spreads", -3.5, 3.5)
            + _q(t, "c", -110, -110, "spreads", -3.5, 3.5) + _q(t, "d", -150, 130, "spreads", -3.0, 3.0))
    sc = sharpness.score_event(rows, "spreads", now=NOW)
    assert "d" not in sc                      # dealt -3 all game: a different bet
    assert set(sc) == {"a", "b", "c"}


def test_score_event_needs_a_leave_one_out_close():
    t = "2026-09-01T10:00:00+00:00"
    rows = _q(t, "a", -110, -110) + _q(t, "b", -120, 100)
    assert sharpness.score_event(rows, "h2h", now=NOW) is None   # one other book is not a close


def test_book_weights_known_values():
    cfg = sharpness.SharpnessConfig(min_book_events=5, prior_events=20)
    scores = {"a": {"events": 30, "mean_kl": 0.001}, "b": {"events": 30, "mean_kl": 0.002},
              "c": {"events": 30, "mean_kl": 0.004}, "new": {"events": 2, "mean_kl": 0.0001}}
    w = sharpness.book_weights(scores, cfg)
    mean_inv = (1000 + 500 + 250) / 3
    for b, inv in (("a", 1000), ("b", 500), ("c", 250)):
        raw = inv / mean_inv
        assert w[b]["raw_weight"] == pytest.approx(raw, abs=1e-4)
        assert w[b]["weight"] == pytest.approx(1 + 0.6 * (raw - 1), abs=1e-4)
    assert w["new"] == {"raw_weight": None, "weight": 1.0}     # too few games to count


def test_book_weights_are_clipped():
    cfg = sharpness.SharpnessConfig(min_book_events=1, prior_events=0.001, weight_min=0.5, weight_max=2.0)
    w = sharpness.book_weights({"a": {"events": 50, "mean_kl": 1e-4},
                                "b": {"events": 50, "mean_kl": 1.0}}, cfg)
    assert w["a"]["weight"] == pytest.approx(2.0, abs=1e-3) and w["b"]["weight"] == 0.5


def test_demo_sharpness_recovers_the_known_book_order():
    rows = demo_adaptive.demo_sharpness_rows("nfl")
    rep = sharpness.sharpness_report(rows, "nfl", now=NOW)
    want = sorted(demo_adaptive.BOOK_NOISE, key=demo_adaptive.BOOK_NOISE.get)
    for market in ("h2h", "spreads", "totals"):
        m = rep["markets"][market]
        assert m["status"] == "ok" and m["events"] == demo_adaptive.HISTORY_GAMES
        assert [b["book"] for b in m["books"]] == want, market
        w = m["weights"]
        assert w["draftkings"] > 1.0 > w["caesars"]
        assert all(0.25 <= v <= 4.0 for v in w.values())


def test_thin_history_falls_back_to_no_weights():
    rows = demo_adaptive.demo_sharpness_rows("nfl")
    rep = sharpness.sharpness_report(rows, "nfl", now=NOW,
                                     config=sharpness.SharpnessConfig(min_events=31))
    assert all(m["status"] == "thin" and m["weights"] is None for m in rep["markets"].values())
    assert sharpness.weights_for_board(rep) == {}
    empty = sharpness.sharpness_report([], "nfl", now=NOW)
    assert empty["markets"]["h2h"]["events"] == 0 and empty["markets"]["h2h"]["books"] == []


def test_sharpness_config_from_env(monkeypatch):
    monkeypatch.setenv("SHARP_MIN_EVENTS", "7")
    monkeypatch.setenv("SHARP_WEIGHT_MAX", "junk")
    cfg = sharpness.SharpnessConfig.from_env()
    assert cfg.min_events == 7 and cfg.weight_max == 4.0


# ---------------- weights on the board ----------------

def test_fair_estimate_weights_only_touch_the_consensus_path():
    books = {"a": [-150, 130], "b": [-140, 120], "c": [-160, 135]}
    w = {"a": 3.0, "b": 1.0, "c": 0.5}
    fair, src = fair_estimate(books, weights=w)
    assert src == "weighted consensus:3 books"
    assert fair == pytest.approx(weighted_consensus_fair_probs(books, w))
    assert fair_estimate(books, weights=None) == fair_estimate(books)
    # a sharp book still wins, and a 2-book pool is still a thin plain consensus
    assert fair_estimate(books, sharp_book="a", weights=w)[1] == "sharp:a"
    assert fair_estimate(books, exclude="c", weights=w)[1].startswith("thin consensus")


def test_weighted_leave_one_out_never_contains_the_graded_book():
    books = {"fd": [-105, -115], "a": [-110, -110], "b": [-112, -108], "c": [-108, -112]}
    w = {"fd": 4.0, "a": 1.0, "b": 0.5, "c": 2.0}
    res = analyse_market(books, ["H", "A"], ["fd"], 10, weights=w)
    want = weighted_consensus_fair_probs({k: v for k, v in books.items() if k != "fd"}, w)
    assert res["sides"][0]["your_books"][0]["fair_prob"] == pytest.approx(want[0], abs=1e-5)
    assert res["sides"][0]["your_books"][0]["fair_source"] == "weighted consensus:3 books (excl. fd)"


def test_board_weighting_is_opt_in(demo_client):
    plain = demo_client.get("/api/board/nfl").json()
    assert plain["weighting"]["mode"] == "equal" and plain["weighting"]["weighted_markets"] == []
    eq = demo_client.get("/api/board/nfl?weighting=equal").json()
    sharp = demo_client.get("/api/board/nfl?weighting=sharp").json()
    assert sharp["weighting"]["weighted_markets"] == ["h2h", "spreads", "totals"]
    g_plain = next(g for g in plain["games"] if g["odds_id"] == "evt_kc_buf")
    g_eq = next(g for g in eq["games"] if g["odds_id"] == "evt_kc_buf")
    g_sh = next(g for g in sharp["games"] if g["odds_id"] == "evt_kc_buf")
    assert g_plain["markets"] == g_eq["markets"]
    m = g_sh["markets"]["h2h"]
    assert m["fair_source"].startswith("weighted consensus")
    assert m["sides"][0]["fair_prob"] != g_plain["markets"]["h2h"]["sides"][0]["fair_prob"]
    assert demo_client.get("/api/board/nfl?weighting=nope").status_code == 400
    assert demo_client.get("/api/board?sport=nfl&weighting=sharp").json()["weighting"]["mode"] == "sharp"


def test_board_sharp_weighting_with_thin_history_is_the_plain_board(demo_client, monkeypatch):
    monkeypatch.setenv("SHARP_MIN_EVENTS", "1000")
    plain = demo_client.get("/api/board/mlb").json()
    sharp = demo_client.get("/api/board/mlb?weighting=sharp").json()
    assert sharp["weighting"]["weighted_markets"] == []
    assert set(sharp["weighting"]["thin_markets"]) == {"h2h", "spreads", "totals"}
    assert [g["markets"] for g in sharp["games"]] == [g["markets"] for g in plain["games"]]


def test_fair_weighting_env_default(demo_client, monkeypatch):
    monkeypatch.setattr(main, "FAIR_WEIGHTING", "sharp")
    assert demo_client.get("/api/board/nfl").json()["weighting"]["mode"] == "sharp"
    assert demo_client.get("/api/board/nfl?weighting=equal").json()["weighting"]["weighted_markets"] == []


# ---------------- /api/sharpness ----------------

def test_sharpness_route_demo(demo_client):
    r = demo_client.get("/api/sharpness/nfl?market=spreads")
    assert r.status_code == 200
    j = r.json()
    assert list(j["markets"]) == ["spreads"]
    assert j["source"] == demo_adaptive.LABEL and j["demo_mode"] is True
    assert j["markets"]["spreads"]["books"][0]["book"] == "draftkings"
    assert "config" in j and "metric" in j
    assert demo_client.get("/api/sharpness/nfl?market=player_points").status_code == 400
    assert demo_client.get("/api/sharpness/nfl?method=bogus").status_code == 400
    assert demo_client.get("/api/sharpness/xfl").status_code == 400
    # a demo league with no fixture: no history, honest thin state
    assert demo_client.get("/api/sharpness/wnba").json()["markets"]["h2h"]["status"] == "thin"


def test_sharpness_route_live_reads_the_history_store(live_client):
    store = main.history_store()
    rows = demo_adaptive.demo_sharpness_rows("nhl")
    by_ts: dict = {}
    for r in rows:
        by_ts.setdefault(r["ts"], []).append(r)
    for ts, rs in sorted(by_ts.items()):
        store.record("nhl", rs, ts)
    # the store writes changes only, so an unchanged price adds nothing
    assert 0 < len(store.league_rows("nhl")) <= len(rows)
    assert store.league_rows("nfl") == []
    j = live_client.get("/api/sharpness/nhl").json()
    assert j["source"] == "local snapshots of The Odds API"
    assert j["markets"]["h2h"]["status"] == "ok"
    assert j["markets"]["h2h"]["books"][0]["book"] == "draftkings"
    assert live_client.get("/api/sharpness/nfl").json()["markets"]["totals"]["events"] == 0


def test_history_league_rows_without_a_file(tmp_path):
    assert history.HistoryStore(str(tmp_path / "none.sqlite3")).league_rows("nfl") == []


# ---------------- calibration ----------------

def test_reliability_bucket_known_values():
    rows = reliability([0.55] * 20 + [0.25] * 4, [1] * 11 + [0] * 9 + [1, 0, 0, 0], bins=10)
    assert [(r["lo"], r["n"]) for r in rows] == [(0.2, 4), (0.5, 20)]
    hi = rows[1]
    assert hi["mean_pred"] == pytest.approx(0.55) and hi["observed"] == pytest.approx(0.55)
    assert hi["gap"] == pytest.approx(0.0) and hi["inside_ci"] and hi["status"] == "ok"
    assert rows[0]["status"] == "thin"
    assert reliability([1.0], [1], bins=4)[0]["hi"] == 1.0       # p=1 lands in the top bucket


def test_edge_buckets():
    assert edge_bucket(-0.01) == "<0%"
    assert edge_bucket(0.0) == "0-1%"
    assert edge_bucket(0.025) == "2-3%"
    assert edge_bucket(0.2) == "5%+"
    assert edge_bucket(None) is None


def test_normalise_play_derives_ev_and_clv():
    p = normalise_play({"price": 100, "fair_prob": 0.52, "close_fair_prob": 0.55,
                        "point": -3.5, "close_point": -3.5, "result": "WON"})
    assert p["ev"] == pytest.approx(0.04)
    assert p["clv"] == pytest.approx(0.10)
    assert p["result"] == "won"
    moved = normalise_play({"price": 100, "close_fair_prob": 0.55, "point": -3.5, "close_point": -3.0})
    assert moved["clv"] is None and moved["line_moved"]
    assert normalise_play({"price": 50}) is None                  # not a real American price
    assert normalise_play({"price": 120, "fair_prob": 1.4})["fair_prob"] is None


def test_calibration_report_known_small_set():
    plays = [
        {"league": "nfl", "market": "spreads", "price": 100, "fair_prob": 0.6, "result": "won",
         "close_fair_prob": 0.55, "stake": 10},
        {"league": "nfl", "market": "spreads", "price": 100, "fair_prob": 0.6, "result": "lost",
         "close_fair_prob": 0.45, "stake": 10},
        {"league": "nfl", "market": "h2h", "price": 150, "fair_prob": 0.45, "result": "push",
         "stake": 10},
        {"league": "nba", "market": "totals", "price": -110, "fair_prob": 0.5, "result": None},
    ]
    r = calibration_report(plays)
    assert r["status"] == "not_enough_data" and "note" in r
    assert (r["plays"], r["graded"], r["pushes"]) == (4, 2, 1)
    assert r["entry"]["brier"] == pytest.approx((0.16 + 0.36) / 2)
    assert r["entry"]["base_rate"] == 0.5
    assert r["entry"]["brier_base_rate"] == pytest.approx(0.25)
    assert r["close"]["brier"] == pytest.approx((0.2025 + 0.2025) / 2)
    o = r["clv"]["overall"]
    assert o["with_clv"] == 2 and o["mean_clv"] == pytest.approx(0.0)
    assert o["record"] == {"won": 1, "lost": 1, "push": 1}
    assert o["profit"] == 0.0 and o["roi"] == 0.0
    keys = [g["key"] for g in r["clv"]["by_league_market"]]
    assert keys == ["nba/totals", "nfl/h2h", "nfl/spreads"]
    assert calibration_report([])["status"] == "no_plays"


def test_demo_calibration_report_is_ok_and_clv_rises_with_edge():
    r = calibration_report(demo_adaptive.demo_tracked_plays())
    assert r["status"] == "ok"
    assert 0.2 < r["entry"]["brier"] < 0.26
    by_edge = r["clv"]["by_edge"]
    assert [g["key"] for g in by_edge] == ["0-1%", "1-2%", "2-3%", "3-5%", "5%+"]
    clvs = [g["mean_clv"] for g in by_edge]
    assert clvs == sorted(clvs)              # the fixture's edges are real but overstated
    assert clvs[0] < 0 < clvs[-1]
    # the tool's entry fair price is biased high, so it predicts more wins than happen
    assert r["entry"]["mean_pred"] > r["entry"]["base_rate"]


def test_calibration_routes(demo_client):
    j = demo_client.get("/api/calibration").json()
    assert j["status"] == "ok" and j["source"] == demo_adaptive.LABEL
    tracker_play = {   # the tracker's own localStorage shape
        "id": "k1", "league": "nfl", "market": "spreads", "side": "Buffalo Bills", "point": -3.5,
        "book": "fanduel", "price": -105, "fair_prob": 0.53, "ev_per_dollar": 0.0348, "stake": 10,
        "closed": True, "close": {"price": -115, "point": -3.5, "fair_prob": 0.545},
        "outcome": None, "result": {"result": "won", "status": "final"}, "demo": True,
    }
    j = demo_client.post("/api/calibration", json={"plays": [tracker_play]}).json()
    assert j["source"] == "your tracked plays" and j["graded"] == 1
    assert j["clv"]["overall"]["mean_clv"] == pytest.approx(clv_vs_fair(-105, 0.545), abs=1e-5)
    assert demo_client.post("/api/calibration", json={"plays": [{"league": "nfl"}]}).status_code == 422
    assert demo_client.post("/api/calibration", json={"bins": 1}).status_code == 422


def test_calibration_live_without_plays(live_client):
    j = live_client.get("/api/calibration").json()
    assert j["status"] == "no_plays" and j["plays"] == 0


# ---------------- thresholds ----------------

def _clv_plays(f, n=81, league="nfl", market="spreads"):
    """Plays with ev spread evenly over 0..8% and clv = f(ev, i)."""
    out = []
    for i in range(n):
        ev = 0.08 * i / (n - 1)
        fair = 0.5
        price = round(((1 + ev) / fair - 1) * 100)
        price = price if price >= 100 else -round(100 / ((1 + ev) / fair - 1))
        out.append({"league": league, "market": market, "price": price, "fair_prob": fair,
                    "ev_per_dollar": ev, "close_fair_prob": None, "_clv": f(ev, i)})
    return out


def _norm(plays):
    out = []
    for p in plays:
        q = normalise_play(p)
        q["ev"], q["clv"] = p["ev_per_dollar"], p["_clv"]
        out.append(q)
    return out


def test_threshold_is_the_break_even_edge_rounded_up():
    plays = _norm(_clv_plays(lambda ev, i: ev - 0.0175 + (0.004 if i % 2 else -0.004)))
    s = suggest_for(plays, ThresholdConfig())
    assert s["status"] == "suggested"
    assert s["fit"]["break_even_edge"] == pytest.approx(0.0175, abs=0.001)
    assert s["suggested_min_edge"] == 0.02
    assert s["basis"]["min_edge"] == 0.02 and s["basis"]["clv_lower"] > 0
    assert len(s["curve"]) == len(ThresholdConfig().grid)


def test_threshold_is_zero_when_every_shown_edge_beats_the_close():
    plays = _norm(_clv_plays(lambda ev, i: ev + 0.01 + (0.002 if i % 2 else -0.002)))
    assert suggest_for(plays, ThresholdConfig())["suggested_min_edge"] == 0.0


def test_threshold_not_enough_data():
    plays = _norm(_clv_plays(lambda ev, i: ev, n=39))
    s = suggest_for(plays, ThresholdConfig())
    assert s["status"] == "not_enough_data" and s["needed"] == 1 and s["suggested_min_edge"] is None


def test_threshold_when_edge_does_not_predict_clv():
    plays = _norm(_clv_plays(lambda ev, i: 0.01 if (i * 7) % 3 else -0.01))
    s = suggest_for(plays, ThresholdConfig())
    assert s["status"] == "edge_not_predictive" and s["suggested_min_edge"] is None


def test_threshold_when_nothing_beats_the_close():
    plays = _norm(_clv_plays(lambda ev, i: ev - 0.2 + (0.004 if i % 2 else -0.004)))
    s = suggest_for(plays, ThresholdConfig())
    assert s["status"] == "no_threshold_beats_close" and s["suggested_min_edge"] is None


def test_demo_suggestions_track_each_leagues_overstatement():
    s = {(x["league"], x["market"]): x for x in suggest_thresholds(demo_adaptive.demo_tracked_plays())["suggestions"]}
    assert s[("ncaaf", "spreads")]["status"] == "not_enough_data"
    assert s[("mlb", "totals")]["status"] == "not_enough_data"
    assert s[("nfl", "spreads")]["status"] == "suggested"
    # NHL's fair price overstates edges most, so it needs the biggest edge
    assert s[("nhl", "h2h")]["suggested_min_edge"] > s[("nfl", "spreads")]["suggested_min_edge"]
    assert all(x["suggested_min_edge"] is None or 0 <= x["suggested_min_edge"] <= 0.08 for x in s.values())


def test_threshold_store_round_trip(tmp_path):
    st = ThresholdStore(str(tmp_path / "t.json"))
    assert st.load() == {} and st.for_league("nfl") == {}
    st.adopt("nfl", "spreads", 0.015, "from the suggester")
    assert ThresholdStore(st.path).for_league("nfl") == {"spreads": 0.015}   # persisted
    assert st.remove("nfl", "spreads") and not st.remove("nfl", "spreads")
    assert st.load() == {}
    mem = ThresholdStore(None)
    mem.adopt("nba", "totals", 0.03)
    assert mem.for_league("nba") == {"totals": 0.03}
    (tmp_path / "bad.json").write_text("not json")
    assert ThresholdStore(str(tmp_path / "bad.json")).load() == {}


def test_threshold_routes_and_board_opt_in(demo_client):
    sug = demo_client.post("/api/thresholds/suggest", json={}).json()
    assert sug["applied"] is False and sug["adopted"] == {}
    assert sug["source"] == demo_adaptive.LABEL
    before = demo_client.get("/api/board/nfl").json()
    assert before["edge_profile"]["mode"] == "none"
    n_before = len(before["plays"])
    assert n_before > 0

    big = demo_client.put("/api/thresholds/nfl/h2h", json={"min_edge": 0.25})
    assert big.status_code == 200 and big.json()["min_edge"] == 0.25
    for m in ("spreads", "totals"):
        demo_client.put(f"/api/thresholds/nfl/{m}", json={"min_edge": 0.25})
    assert demo_client.get("/api/thresholds").json()["adopted"]["nfl"]["h2h"]["min_edge"] == 0.25

    # adopting alone changes nothing on the board
    assert len(demo_client.get("/api/board/nfl").json()["plays"]) == n_before
    after = demo_client.get("/api/board/nfl?edge_profile=adopted").json()
    assert after["plays"] == [] and after["edge_profile"]["min_edges"]["h2h"] == 0.25
    demo_client.delete("/api/thresholds/nfl/h2h")
    assert demo_client.delete("/api/thresholds/nfl/h2h").status_code == 404
    assert demo_client.put("/api/thresholds/nfl/h2h", json={"min_edge": 0.9}).status_code == 422
    assert demo_client.put("/api/thresholds/nfl/BAD KEY", json={"min_edge": 0.01}).status_code == 400
    assert demo_client.put("/api/thresholds/xfl/h2h", json={"min_edge": 0.01}).status_code == 400
    assert demo_client.get("/api/board/nfl?edge_profile=maybe").status_code == 400


def test_adopted_thresholds_persist_live_and_apply_by_env(live_client, monkeypatch):
    live_client.put("/api/thresholds/nfl/spreads", json={"min_edge": 0.02})
    with open(main.THRESHOLDS_FILE) as fh:
        assert "spreads" in fh.read()
    monkeypatch.setattr(main, "APPLY_ADOPTED_THRESHOLDS", True)
    j = live_client.get("/api/board/nfl").json()      # no key: schedule-only, but the profile is reported
    assert j["edge_profile"] == {"mode": "adopted", "min_edges": {"spreads": 0.02}, "default_min_edge": 0.0}
    assert live_client.get("/api/board/nfl?edge_profile=none").json()["edge_profile"]["min_edges"] == {}
