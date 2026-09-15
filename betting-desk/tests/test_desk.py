"""Unit tests for config, feed, analysis and the HTTP layer."""
from __future__ import annotations

import json
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
from http.server import ThreadingHTTPServer
from pathlib import Path

from desk import odds
from desk.analyze import analyze_all, analyze_market, build_parlay
from desk.config import Settings, load_dotenv, load_settings
from desk.feed import (DEMO_MARKETS, FeedError, load_demo, load_file,
                       load_markets, validate_markets)
from desk.server import Desk, Handler

DEMO = {"DEMO_MODE": "1"}


class TestConfig(unittest.TestCase):

    def test_dotenv_parses_comments_quotes_and_blanks(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / ".env"
            p.write_text(
                "# a comment\n"
                "\n"
                "DEMO_MODE=1\n"
                'SHARP_BOOK="pinnacle"\n'
                "YOUR_BOOKS = fanduel, betmgm \n"
                "junkline\n"
            )
            got = load_dotenv(p)
        self.assertEqual(got["DEMO_MODE"], "1")
        self.assertEqual(got["SHARP_BOOK"], "pinnacle")
        self.assertEqual(got["YOUR_BOOKS"], "fanduel, betmgm")
        self.assertNotIn("junkline", got)

    def test_missing_dotenv_is_not_an_error(self):
        self.assertEqual(load_dotenv(Path("/nonexistent/.env")), {})

    def test_demo_mode_truthiness(self):
        for v in ("1", "true", "TRUE", "yes", "on"):
            self.assertTrue(load_settings({"DEMO_MODE": v}).demo_mode, v)
        for v in ("0", "false", "no", "off", ""):
            self.assertFalse(load_settings({"DEMO_MODE": v}).demo_mode, v)

    def test_demo_mode_overrides_feed_source(self):
        s = load_settings({"DEMO_MODE": "1", "FEED_SOURCE": "oddsapi"})
        self.assertEqual(s.effective_source, "demo")

    def test_live_mode_respects_feed_source(self):
        s = load_settings({"DEMO_MODE": "0", "FEED_SOURCE": "file"})
        self.assertEqual(s.effective_source, "file")

    def test_books_parse_as_csv(self):
        s = load_settings(dict(DEMO, YOUR_BOOKS="fanduel, betmgm ,caesars"))
        self.assertEqual(s.your_books, ["fanduel", "betmgm", "caesars"])

    def test_unknown_devig_method_rejected(self):
        with self.assertRaises(ValueError):
            load_settings(dict(DEMO, DEVIG_METHOD="vibes"))

    def test_sharp_book_cannot_be_one_of_your_books(self):
        # Otherwise the desk grades a price against itself and every row
        # comes back as exactly that book's vig.
        with self.assertRaises(ValueError):
            load_settings(dict(DEMO, YOUR_BOOKS="fanduel,pinnacle",
                               SHARP_BOOK="pinnacle"))

    def test_empty_sharp_book_is_allowed(self):
        s = load_settings(dict(DEMO, SHARP_BOOK=""))
        self.assertEqual(s.sharp_book, "")

    def test_numeric_settings_are_typed(self):
        s = load_settings(dict(DEMO, PORT="9001", BANKROLL="500",
                               KELLY_MULTIPLIER="0.5", MIN_EV="-1"))
        self.assertEqual(s.port, 9001)
        self.assertEqual(s.bankroll, 500.0)
        self.assertEqual(s.kelly_multiplier, 0.5)
        self.assertEqual(s.min_ev, -1.0)


class TestFeed(unittest.TestCase):

    def test_demo_fixtures_are_valid(self):
        self.assertEqual(len(load_demo()), len(DEMO_MARKETS))

    def test_demo_is_deterministic(self):
        self.assertEqual(load_demo(), load_demo())

    def test_every_fixture_has_a_bettable_and_a_benchmark_book(self):
        s = load_settings(DEMO)
        for m in load_demo():
            with self.subTest(market=m["id"]):
                self.assertTrue(
                    any(m["books"].get(b) for b in s.your_books)
                    or m["id"] == "nfl-kc-buf-anytime-td",
                    "fixture is unreachable from YOUR_BOOKS",
                )

    def test_fixtures_include_a_consensus_only_market(self):
        # The consensus fallback needs a market with no sharp price, or it
        # is never exercised.
        s = load_settings(DEMO)
        self.assertTrue(any(not m["books"].get(s.sharp_book) for m in load_demo()))

    def test_fixtures_include_a_three_way_market(self):
        self.assertTrue(any(len(m["outcomes"]) == 3 for m in load_demo()))

    def test_validate_rejects_missing_keys(self):
        with self.assertRaises(FeedError):
            validate_markets([{"id": "x"}])

    def test_validate_rejects_ragged_book(self):
        bad = [dict(DEMO_MARKETS[0], books={"a": [-110, -110], "b": [-110]})]
        with self.assertRaises(FeedError):
            validate_markets(bad)

    def test_validate_rejects_impossible_price(self):
        # Nothing lies between -100 and +100 in american odds.
        bad = [dict(DEMO_MARKETS[0], books={"a": [50, -110]})]
        with self.assertRaises(FeedError) as cm:
            validate_markets(bad)
        self.assertIn("not a valid american price", str(cm.exception))

    def test_validate_rejects_single_outcome(self):
        bad = [dict(DEMO_MARKETS[0], outcomes=["only"], books={"a": [-110]})]
        with self.assertRaises(FeedError):
            validate_markets(bad)

    def test_validate_rejects_bookless_market(self):
        with self.assertRaises(FeedError):
            validate_markets([dict(DEMO_MARKETS[0], books={})])

    def test_file_source_roundtrips(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "snap.json"
            p.write_text(json.dumps({"markets": DEMO_MARKETS}))
            got = load_file(p)
        self.assertEqual(len(got), len(DEMO_MARKETS))

    def test_file_source_accepts_a_bare_list(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "snap.json"
            p.write_text(json.dumps(DEMO_MARKETS))
            self.assertEqual(len(load_file(p)), len(DEMO_MARKETS))

    def test_missing_file_raises_feed_error(self):
        with self.assertRaises(FeedError):
            load_file("/nonexistent/snap.json")

    def test_malformed_json_raises_feed_error(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "snap.json"
            p.write_text("{not json")
            with self.assertRaises(FeedError):
                load_file(p)

    def test_file_source_needs_a_path(self):
        s = load_settings({"DEMO_MODE": "0", "FEED_SOURCE": "file", "FEED_FILE": ""})
        with self.assertRaises(FeedError):
            load_markets(s)

    def test_oddsapi_without_key_fails_before_any_network_call(self):
        s = load_settings({"DEMO_MODE": "0", "FEED_SOURCE": "oddsapi",
                           "ODDS_API_KEY": ""})
        with self.assertRaises(FeedError) as cm:
            load_markets(s)
        self.assertIn("ODDS_API_KEY", str(cm.exception))

    def test_unknown_source_raises(self):
        s = load_settings({"DEMO_MODE": "0", "FEED_SOURCE": "telepathy"})
        with self.assertRaises(FeedError):
            load_markets(s)


class TestAnalyze(unittest.TestCase):

    def setUp(self):
        self.s = load_settings(DEMO)
        self.markets = load_demo()
        self.by_id = {m["id"]: m for m in self.markets}

    def test_rows_are_one_per_bettable_book_per_outcome(self):
        m = self.by_id["nfl-kc-buf-ml"]
        rows = analyze_market(m, self.s)
        self.assertEqual(len(rows), 2 * 2)  # 2 books x 2 outcomes

    def test_market_your_books_do_not_quote_yields_nothing(self):
        m = dict(self.by_id["nfl-kc-buf-ml"], books={"pinnacle": [-150, 135]})
        self.assertEqual(analyze_market(m, self.s), [])

    def test_sharp_price_is_preferred_when_present(self):
        rows = analyze_market(self.by_id["nfl-kc-buf-ml"], self.s)
        self.assertTrue(all(r["fair_source"] == "sharp:pinnacle" for r in rows))

    def test_consensus_used_when_sharp_book_absent(self):
        rows = analyze_market(self.by_id["mlb-nyy-lad-total"], self.s)
        self.assertTrue(rows)
        self.assertTrue(all(r["fair_source"] == "consensus" for r in rows))

    def test_kelly_stake_is_fractional_kelly_of_bankroll(self):
        rows = analyze_market(self.by_id["nfl-kc-buf-ml"], self.s)
        r = max(rows, key=lambda r: r["kelly"])
        self.assertAlmostEqual(
            r["kelly_stake"],
            round(self.s.bankroll * r["kelly"] * self.s.kelly_multiplier, 2),
        )

    def test_no_edge_means_no_stake(self):
        rows = analyze_market(self.by_id["nfl-kc-buf-ml"], self.s)
        for r in rows:
            if not r["has_edge"]:
                self.assertEqual(r["kelly_stake"], 0.0)

    def test_beat_by_best_flags_a_better_price_elsewhere(self):
        rows = analyze_market(self.by_id["nba-lal-bos-ml"], self.s)
        for r in rows:
            self.assertEqual(r["beat_by_best"], r["best_price"] != r["your_price"])

    def test_analyze_all_sorts_by_ev_descending(self):
        out = analyze_all(self.markets, self.s, min_ev=-10)
        evs = [r["ev_per_dollar"] for r in out["rows"]]
        self.assertEqual(evs, sorted(evs, reverse=True))

    def test_min_ev_filters(self):
        loose = analyze_all(self.markets, self.s, min_ev=-10)
        tight = analyze_all(self.markets, self.s, min_ev=0.0)
        self.assertLess(tight["shown"], loose["shown"])
        self.assertTrue(all(r["ev_per_dollar"] >= 0 for r in tight["rows"]))

    def test_total_rows_is_unfiltered(self):
        loose = analyze_all(self.markets, self.s, min_ev=-10)
        tight = analyze_all(self.markets, self.s, min_ev=0.0)
        self.assertEqual(loose["total_rows"], tight["total_rows"])

    def test_demo_feed_yields_at_least_one_real_edge(self):
        # If the fixtures ever go flat the UI has nothing to demonstrate.
        out = analyze_all(self.markets, self.s, min_ev=0.0)
        self.assertGreaterEqual(out["positive_ev"], 1)

    def test_most_prices_are_minus_ev(self):
        # Sanity check on the whole pipeline: a real book beats you on most
        # lines. A feed where everything is +EV means the math is inverted.
        out = analyze_all(self.markets, self.s, min_ev=-10)
        self.assertLess(out["positive_ev"], out["total_rows"] / 2)

    def test_consensus_only_markets_are_reported(self):
        out = analyze_all(self.markets, self.s, min_ev=-10)
        self.assertIn("mlb-nyy-lad-total", out["consensus_only_markets"])

    def test_method_override_changes_the_numbers(self):
        a = analyze_all(self.markets, self.s, min_ev=-10, method="power")
        b = analyze_all(self.markets, self.s, min_ev=-10, method="multiplicative")
        self.assertNotEqual([r["fair_prob"] for r in a["rows"]],
                            [r["fair_prob"] for r in b["rows"]])

    def test_stake_override_scales_ev_dollars_only(self):
        a = analyze_all(self.markets, self.s, min_ev=-10, stake=10.0)["rows"][0]
        b = analyze_all(self.markets, self.s, min_ev=-10, stake=100.0)["rows"][0]
        self.assertAlmostEqual(b["ev_dollars"], a["ev_dollars"] * 10, places=6)
        self.assertAlmostEqual(a["ev_per_dollar"], b["ev_per_dollar"], places=12)


class TestBuildParlay(unittest.TestCase):

    def setUp(self):
        self.s = load_settings(DEMO)
        self.markets = load_demo()

    def leg(self, market_id, i=0, book="fanduel"):
        return {"market_id": market_id, "outcome_index": i, "book": book}

    def test_two_leg_parlay_prices_and_grades(self):
        r = build_parlay(
            [self.leg("nfl-kc-buf-ml", 0), self.leg("nba-lal-bos-ml", 1)],
            self.markets, self.s, stake=50.0,
        )
        self.assertEqual(r["legs"], 2)
        self.assertIsNotNone(r["ev_dollars"])
        self.assertFalse(r["same_game"])
        self.assertEqual(len(r["legs_detail"]), 2)

    def test_price_matches_the_named_book(self):
        r = build_parlay([self.leg("nfl-kc-buf-ml", 0, "fanduel")],
                         self.markets, self.s)
        self.assertEqual(r["legs_detail"][0]["american"], -140)

    def test_same_game_legs_are_flagged(self):
        r = build_parlay(
            [self.leg("nfl-kc-buf-ml", 0), self.leg("nfl-kc-buf-spread", 0)],
            self.markets, self.s,
        )
        self.assertTrue(r["same_game"])
        self.assertTrue(any("correlated" in n for n in r["notes"]))

    def test_parlay_hold_is_positive_and_beats_a_single_leg(self):
        one = build_parlay([self.leg("nfl-kc-buf-ml", 1)], self.markets, self.s)
        two = build_parlay(
            [self.leg("nfl-kc-buf-ml", 1), self.leg("nba-lal-bos-ml", 1)],
            self.markets, self.s,
        )
        self.assertGreater(one["total_hold"], 0.0)
        self.assertGreater(two["total_hold"], one["total_hold"])

    def test_unknown_market_raises(self):
        with self.assertRaises(ValueError):
            build_parlay([self.leg("nope")], self.markets, self.s)

    def test_out_of_range_outcome_raises(self):
        with self.assertRaises(ValueError):
            build_parlay([self.leg("nfl-kc-buf-ml", 7)], self.markets, self.s)

    def test_book_that_does_not_quote_raises(self):
        with self.assertRaises(ValueError):
            build_parlay([self.leg("mlb-nyy-lad-total", 0, "pinnacle")],
                         self.markets, self.s)

    def test_book_defaults_to_first_of_your_books(self):
        r = build_parlay([{"market_id": "nfl-kc-buf-ml", "outcome_index": 0}],
                         self.markets, self.s)
        self.assertEqual(r["legs_detail"][0]["book"], "fanduel")

    def test_empty_legs(self):
        # parlay_report short-circuits to {"legs": 0}; build_parlay still
        # attaches the (empty) leg detail so clients can render uniformly.
        self.assertEqual(
            build_parlay([], self.markets, self.s),
            {"legs": 0, "legs_detail": [], "duplicate_outcomes": []},
        )

    def test_duplicate_outcome_is_called_out(self):
        # Same outcome, two books. P(both) = P(one), not P squared.
        r = build_parlay(
            [self.leg("nfl-kc-buf-anytime-td", 1, "fanduel"),
             self.leg("nfl-kc-buf-anytime-td", 1, "draftkings")],
            self.markets, self.s,
        )
        self.assertEqual(r["duplicate_outcomes"], ["No TD (2x)"])
        self.assertTrue(any("does not compound" in n for n in r["notes"]))

    def test_distinct_outcomes_are_not_flagged_as_duplicates(self):
        r = build_parlay(
            [self.leg("nfl-kc-buf-ml", 0), self.leg("nba-lal-bos-ml", 1)],
            self.markets, self.s,
        )
        self.assertEqual(r["duplicate_outcomes"], [])

    def test_opposite_sides_of_one_market_are_not_duplicates(self):
        # Contradictory, but not the same outcome twice; same_game covers it.
        r = build_parlay(
            [self.leg("nfl-kc-buf-ml", 0), self.leg("nfl-kc-buf-ml", 1)],
            self.markets, self.s,
        )
        self.assertEqual(r["duplicate_outcomes"], [])
        self.assertTrue(r["same_game"])

    def test_leg_from_a_consensus_market_still_grades(self):
        r = build_parlay([self.leg("mlb-nyy-lad-total", 0)], self.markets, self.s)
        self.assertIsNotNone(r["legs_detail"][0]["fair_prob"])


class TestServer(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        settings = load_settings(dict(DEMO, HOST="127.0.0.1", PORT="0"))
        Handler.desk = Desk(settings)
        cls.httpd = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        cls.base = f"http://127.0.0.1:{cls.httpd.server_address[1]}"
        cls.thread = threading.Thread(target=cls.httpd.serve_forever, daemon=True)
        cls.thread.start()

    @classmethod
    def tearDownClass(cls):
        cls.httpd.shutdown()
        cls.httpd.server_close()
        cls.thread.join(timeout=5)

    def get(self, path):
        with urllib.request.urlopen(self.base + path, timeout=10) as r:
            return r.status, json.loads(r.read())

    def get_raw(self, path):
        with urllib.request.urlopen(self.base + path, timeout=10) as r:
            return r.status, r.headers.get("Content-Type"), r.read()

    def post(self, path, payload):
        req = urllib.request.Request(
            self.base + path, data=json.dumps(payload).encode(),
            headers={"Content-Type": "application/json"}, method="POST")
        with urllib.request.urlopen(req, timeout=10) as r:
            return r.status, json.loads(r.read())

    def test_health(self):
        status, body = self.get("/api/health")
        self.assertEqual(status, 200)
        self.assertTrue(body["ok"])
        self.assertTrue(body["demo_mode"])
        self.assertEqual(body["feed_source"], "demo fixtures")
        self.assertEqual(body["markets"], len(DEMO_MARKETS))

    def test_index_is_served(self):
        status, ctype, body = self.get_raw("/")
        self.assertEqual(status, 200)
        self.assertIn("text/html", ctype)
        self.assertIn(b"Betting Desk", body)

    def test_markets(self):
        status, body = self.get("/api/markets")
        self.assertEqual(status, 200)
        self.assertEqual(len(body["markets"]), len(DEMO_MARKETS))

    def test_edges_defaults(self):
        status, body = self.get("/api/edges")
        self.assertEqual(status, 200)
        self.assertIn("rows", body)
        self.assertEqual(body["method"], "power")

    def test_edges_accepts_parameters(self):
        status, body = self.get("/api/edges?stake=250&method=multiplicative&min_ev=-1")
        self.assertEqual(status, 200)
        self.assertEqual(body["method"], "multiplicative")
        self.assertEqual(body["stake"], 250.0)
        self.assertEqual(body["shown"], body["total_rows"])

    def test_edges_rejects_unknown_method(self):
        with self.assertRaises(urllib.error.HTTPError) as cm:
            self.get("/api/edges?method=vibes")
        self.assertEqual(cm.exception.code, 400)

    def test_edges_rejects_non_numeric_stake(self):
        with self.assertRaises(urllib.error.HTTPError) as cm:
            self.get("/api/edges?stake=lots")
        self.assertEqual(cm.exception.code, 400)

    def test_parlay_endpoint(self):
        status, body = self.post("/api/parlay", {
            "stake": 25,
            "legs": [
                {"market_id": "nfl-kc-buf-ml", "outcome_index": 0, "book": "fanduel"},
                {"market_id": "nba-lal-bos-ml", "outcome_index": 1, "book": "fanduel"},
            ],
        })
        self.assertEqual(status, 200)
        self.assertEqual(body["legs"], 2)
        self.assertEqual(body["stake"], 25)

    def test_parlay_rejects_bad_market(self):
        with self.assertRaises(urllib.error.HTTPError) as cm:
            self.post("/api/parlay", {"legs": [{"market_id": "nope"}]})
        self.assertEqual(cm.exception.code, 400)

    def test_parlay_rejects_non_list_legs(self):
        with self.assertRaises(urllib.error.HTTPError) as cm:
            self.post("/api/parlay", {"legs": "two of them"})
        self.assertEqual(cm.exception.code, 400)

    def test_favicon_is_served(self):
        # Browsers request this unprompted; a 404 shows up as a console
        # error on every page load.
        status, ctype, body = self.get_raw("/favicon.ico")
        self.assertEqual(status, 200)
        self.assertIn("image/svg+xml", ctype)
        self.assertIn(b"<svg", body)

    def test_unknown_route_404s(self):
        with self.assertRaises(urllib.error.HTTPError) as cm:
            self.get("/api/moon")
        self.assertEqual(cm.exception.code, 404)

    def test_static_traversal_is_blocked(self):
        for path in ("/static/../desk/odds.py", "/static/%2e%2e/.env"):
            with self.subTest(path=path):
                with self.assertRaises(urllib.error.HTTPError) as cm:
                    self.get_raw(path)
                self.assertIn(cm.exception.code, (400, 404))

    def test_api_json_never_contains_nan(self):
        # allow_nan=False in the handler; a NaN would serialise to invalid
        # JSON and silently poison a client.
        status, ctype, body = self.get_raw("/api/edges?min_ev=-10")
        self.assertEqual(status, 200)
        self.assertNotIn(b"NaN", body)
        json.loads(body)


if __name__ == "__main__":
    unittest.main(verbosity=2)
