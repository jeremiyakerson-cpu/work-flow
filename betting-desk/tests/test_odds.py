"""
Unit tests for desk.odds.

Style: every non-obvious expected number is derived in a comment, so a
failure tells you whether the code or the expectation is wrong.
"""
from __future__ import annotations

import math
import unittest

from desk import odds


class TestFormatConversion(unittest.TestCase):

    def test_even_money_both_signs(self):
        self.assertAlmostEqual(odds.american_to_decimal(100), 2.0)
        self.assertAlmostEqual(odds.american_to_decimal(-100), 2.0)

    def test_underdog_to_decimal(self):
        # +150 pays 1.5x the stake as profit, so 2.5 total.
        self.assertAlmostEqual(odds.american_to_decimal(150), 2.5)

    def test_favourite_to_decimal(self):
        # -150: risk 150 to win 100 -> profit 0.6667x -> 1.6667 total.
        self.assertAlmostEqual(odds.american_to_decimal(-150), 1.0 + 100.0 / 150.0)

    def test_standard_juice_to_decimal(self):
        self.assertAlmostEqual(odds.american_to_decimal(-110), 1.9090909, places=6)

    def test_decimal_to_american_both_sides(self):
        self.assertEqual(odds.decimal_to_american(2.0), 100)
        self.assertEqual(odds.decimal_to_american(2.5), 150)
        self.assertEqual(odds.decimal_to_american(1.0 + 100.0 / 110.0), -110)

    def test_roundtrip_american_decimal_american(self):
        # -100 is excluded on purpose: see the next test.
        for a in list(range(100, 2001)) + [-x for x in range(101, 2001)]:
            with self.subTest(american=a):
                self.assertEqual(
                    odds.decimal_to_american(odds.american_to_decimal(a)), a
                )

    def test_minus_100_normalises_to_plus_100(self):
        # Both are even money (decimal 2.0) and +100 is the canonical
        # spelling, so the roundtrip is not sign-preserving at the boundary.
        self.assertEqual(odds.decimal_to_american(odds.american_to_decimal(-100)), 100)

    def test_implied_probability(self):
        # -110 -> 110/210
        self.assertAlmostEqual(odds.american_to_implied(-110), 110.0 / 210.0)
        # +150 -> 100/250 = 0.4
        self.assertAlmostEqual(odds.american_to_implied(150), 0.4)
        self.assertAlmostEqual(odds.american_to_implied(100), 0.5)
        self.assertAlmostEqual(odds.american_to_implied(-100), 0.5)

    def test_implied_matches_inverse_decimal(self):
        for a in (-2500, -600, -110, -100, 100, 115, 400, 3000):
            with self.subTest(american=a):
                self.assertAlmostEqual(
                    odds.american_to_implied(a),
                    1.0 / odds.american_to_decimal(a),
                )

    def test_implied_to_american(self):
        self.assertEqual(odds.implied_to_american(0.5), 100)
        self.assertEqual(odds.implied_to_american(0.4), 150)

    def test_implied_to_american_rejects_impossible_probabilities(self):
        for p in (0.0, 1.0, -0.1, 1.5):
            with self.subTest(p=p):
                with self.assertRaises(ValueError):
                    odds.implied_to_american(p)


class TestVig(unittest.TestCase):

    def test_overround_of_standard_market(self):
        # 2 * 110/210 - 1 = 0.047619
        self.assertAlmostEqual(odds.overround([-110, -110]), 0.047619, places=6)

    def test_hold_of_standard_market_is_4_545_percent(self):
        # The number books quote. 1 - 1/1.047619 = 0.0454545
        self.assertAlmostEqual(odds.hold([-110, -110]), 0.0454545, places=6)

    def test_hold_is_strictly_below_overround(self):
        # This is the confusion the docstrings warn about: using overround
        # as hold overstates the book's margin on every vigged market.
        for market in ([-110, -110], [-150, 130], [-2000, 1000], [120, 120, 120]):
            with self.subTest(market=market):
                self.assertLess(odds.hold(market), odds.overround(market))

    def test_fair_market_holds_nothing(self):
        # +100/-100 is a fair two-way book.
        self.assertAlmostEqual(odds.hold([100, -100]), 0.0)
        self.assertAlmostEqual(odds.overround([100, -100]), 0.0)


class TestDevig(unittest.TestCase):

    MARKETS = ([-110, -110], [-150, 130], [-2000, 1000],
               [-150, 130, 20000], [120, 130, 140])

    def test_every_method_normalises_to_one(self):
        for method in odds.DEVIG_METHODS:
            for market in self.MARKETS:
                with self.subTest(method=method, market=market):
                    self.assertAlmostEqual(
                        sum(odds.devig(market, method)), 1.0, places=9
                    )

    def test_symmetric_market_splits_evenly_under_every_method(self):
        for method in odds.DEVIG_METHODS:
            with self.subTest(method=method):
                p = odds.devig([-110, -110], method)
                self.assertAlmostEqual(p[0], 0.5, places=9)
                self.assertAlmostEqual(p[1], 0.5, places=9)

    def test_multiplicative_is_proportional(self):
        raw = [odds.american_to_implied(a) for a in (-150, 130)]
        got = odds.devig_multiplicative([-150, 130])
        # Ratios are preserved exactly; that is the defining property.
        self.assertAlmostEqual(got[0] / got[1], raw[0] / raw[1], places=12)

    def test_additive_removes_equal_amounts(self):
        raw = [odds.american_to_implied(a) for a in (-150, 130)]
        got = odds.devig_additive([-150, 130])
        self.assertAlmostEqual(raw[0] - got[0], raw[1] - got[1], places=12)

    def test_additive_can_go_negative_on_a_longshot(self):
        # Documented trap. implied(+20000) = 100/20100 = 0.004975, but the
        # market's excess spread over 3 outcomes is 0.013253 per outcome,
        # so the longshot lands below zero.
        got = odds.devig_additive([-150, 130, 20000])
        self.assertLess(got[2], 0.0)
        with self.assertRaises(ValueError):
            odds.implied_to_american(got[2])

    def test_power_converges(self):
        for market in self.MARKETS:
            with self.subTest(market=market):
                self.assertAlmostEqual(
                    sum(odds.devig_power(market)), 1.0, places=11
                )

    def test_power_shades_the_longshot_below_multiplicative(self):
        # The favourite-longshot correction: on a lopsided market power
        # assigns the longshot less probability than proportional de-vig.
        mult = odds.devig_multiplicative([-2000, 1000])
        powr = odds.devig_power([-2000, 1000])
        self.assertLess(powr[1], mult[1])
        self.assertGreater(powr[0], mult[0])

    def test_power_agrees_with_multiplicative_when_symmetric(self):
        mult = odds.devig_multiplicative([-120, -120])
        powr = odds.devig_power([-120, -120])
        for m, p in zip(mult, powr):
            self.assertAlmostEqual(m, p, places=9)

    def test_devig_rejects_unknown_method(self):
        with self.assertRaises(ValueError):
            odds.devig([-110, -110], "vibes")

    def test_devig_of_fair_market_is_a_no_op(self):
        for method in odds.DEVIG_METHODS:
            with self.subTest(method=method):
                p = odds.devig([100, -100], method)
                self.assertAlmostEqual(p[0], 0.5, places=9)


class TestParlays(unittest.TestCase):

    def test_two_coinflips(self):
        self.assertAlmostEqual(odds.parlay_decimal([100, 100]), 4.0)
        self.assertEqual(odds.parlay_american([100, 100]), 300)

    def test_classic_two_team_parlay(self):
        # 1.909091^2 = 3.644628 -> +264 (books commonly pay +260 to +265)
        self.assertAlmostEqual(odds.parlay_decimal([-110, -110]), 3.6446281, places=6)
        self.assertEqual(odds.parlay_american([-110, -110]), 264)

    def test_single_leg_parlay_is_the_leg(self):
        self.assertEqual(odds.parlay_american([-135]), -135)

    def test_empty_parlay_is_unit(self):
        self.assertAlmostEqual(odds.parlay_decimal([]), 1.0)

    def test_parlay_decimal_is_the_product(self):
        legs = [-110, 150, -200, 320]
        want = math.prod(odds.american_to_decimal(a) for a in legs)
        self.assertAlmostEqual(odds.parlay_decimal(legs), want, places=12)


class TestValue(unittest.TestCase):

    def test_coinflip_at_even_money_is_zero_ev(self):
        self.assertAlmostEqual(odds.expected_value(100, 100, 0.5), 0.0)

    def test_betting_at_the_breakeven_price_is_zero_ev(self):
        for a in (-250, -110, 100, 175, 900):
            with self.subTest(american=a):
                self.assertAlmostEqual(
                    odds.expected_value(10, a, odds.breakeven_prob(a)), 0.0, places=12
                )

    def test_ev_percent_of_a_three_point_edge(self):
        # -110 pays 0.909091. 0.55*0.909091 - 0.45 = 0.05
        self.assertAlmostEqual(odds.ev_percent(-110, 0.55), 0.05, places=9)

    def test_ev_scales_linearly_with_stake(self):
        one = odds.ev_percent(-110, 0.55)
        self.assertAlmostEqual(odds.expected_value(250, -110, 0.55), 250 * one, places=9)

    def test_negative_edge_is_negative_ev(self):
        self.assertLess(odds.ev_percent(-110, 0.50), 0.0)

    def test_kelly_on_a_known_edge(self):
        # b = 0.909091, p = 0.55 -> (0.5 - 0.45)/0.909091 = 0.055
        self.assertAlmostEqual(odds.kelly_fraction(-110, 0.55), 0.055, places=9)

    def test_kelly_even_money_three_quarters(self):
        # b = 1, p = 0.75 -> 0.5 of bankroll. Full Kelly is aggressive.
        self.assertAlmostEqual(odds.kelly_fraction(100, 0.75), 0.5, places=12)

    def test_kelly_is_zero_without_an_edge(self):
        # At exactly breakeven the closed form lands on float noise
        # (~1e-16), not a clean zero; below breakeven the clamp bites.
        self.assertAlmostEqual(
            odds.kelly_fraction(-110, odds.breakeven_prob(-110)), 0.0, places=12
        )
        self.assertEqual(odds.kelly_fraction(-110, 0.40), 0.0)

    def test_kelly_never_negative(self):
        for a in (-400, -110, 100, 600):
            for p in (0.01, 0.2, 0.5, 0.8, 0.99):
                with self.subTest(american=a, p=p):
                    self.assertGreaterEqual(odds.kelly_fraction(a, p), 0.0)

    def test_breakeven_is_the_implied_price(self):
        self.assertAlmostEqual(odds.breakeven_prob(-110), 110.0 / 210.0)


class TestFindEdges(unittest.TestCase):

    OUTCOMES = ["Home", "Away"]
    FEED = {
        "pinnacle":   [-150, 135],
        "fanduel":    [-140, 120],
        "draftkings": [-155, 130],
    }

    def test_sharp_book_is_the_fair_source(self):
        e = odds.find_edges(self.OUTCOMES, self.FEED, ["fanduel"],
                            sharp_book="pinnacle")
        self.assertTrue(all(x.fair_source == "sharp:pinnacle" for x in e))

    def test_sharp_fair_probs_match_devigged_sharp_line(self):
        e = odds.find_edges(self.OUTCOMES, self.FEED, ["fanduel"],
                            sharp_book="pinnacle")
        want = odds.devig(self.FEED["pinnacle"], "power")
        self.assertAlmostEqual(e[0].fair_prob, want[0], places=12)
        self.assertAlmostEqual(e[1].fair_prob, want[1], places=12)

    def test_consensus_excludes_your_own_book(self):
        e = odds.find_edges(self.OUTCOMES, self.FEED, ["fanduel"])
        want = odds.consensus_fair_probs(self.FEED, "power", exclude="fanduel")
        self.assertEqual(e[0].fair_source, "consensus")
        self.assertAlmostEqual(e[0].fair_prob, want[0], places=12)

    def test_best_price_is_the_best_decimal_not_the_biggest_number(self):
        e = odds.find_edges(self.OUTCOMES, self.FEED, ["fanduel"],
                            sharp_book="pinnacle")
        home = next(x for x in e if x.outcome == "Home")
        away = next(x for x in e if x.outcome == "Away")
        # -140 is the shortest favourite price on offer -> best for a bettor.
        self.assertEqual((home.best_book, home.best_price), ("fanduel", -140))
        # +135 beats +130 and +120.
        self.assertEqual((away.best_book, away.best_price), ("pinnacle", 135))

    def test_soft_book_off_the_sharp_line_shows_an_edge(self):
        e = odds.find_edges(self.OUTCOMES, self.FEED, ["fanduel"], stake=100.0,
                            sharp_book="pinnacle")
        home = next(x for x in e if x.outcome == "Home")
        self.assertTrue(home.has_edge)
        self.assertGreater(home.ev_dollars, 0.0)
        self.assertGreater(home.kelly, 0.0)

    def test_ev_dollars_tracks_stake(self):
        ten = odds.find_edges(self.OUTCOMES, self.FEED, ["fanduel"], stake=10.0,
                              sharp_book="pinnacle")[0]
        hundred = odds.find_edges(self.OUTCOMES, self.FEED, ["fanduel"], stake=100.0,
                                  sharp_book="pinnacle")[0]
        self.assertAlmostEqual(hundred.ev_dollars, ten.ev_dollars * 10, places=9)
        self.assertAlmostEqual(ten.ev_per_dollar, hundred.ev_per_dollar, places=12)

    def test_hold_reported_is_your_books_hold(self):
        e = odds.find_edges(self.OUTCOMES, self.FEED, ["fanduel"],
                            sharp_book="pinnacle")
        self.assertAlmostEqual(e[0].hold_at_your_book,
                               odds.hold(self.FEED["fanduel"]), places=12)

    def test_unknown_or_empty_book_is_skipped(self):
        feed = dict(self.FEED, empty=[])
        e = odds.find_edges(self.OUTCOMES, feed, ["nosuchbook", "empty"],
                            sharp_book="pinnacle")
        self.assertEqual(e, [])

    def test_one_row_per_outcome_per_book(self):
        e = odds.find_edges(self.OUTCOMES, self.FEED, ["fanduel", "draftkings"],
                            sharp_book="pinnacle")
        self.assertEqual(len(e), 4)

    def test_edge_to_dict_carries_has_edge(self):
        e = odds.find_edges(self.OUTCOMES, self.FEED, ["fanduel"],
                            sharp_book="pinnacle")[0]
        d = e.to_dict()
        self.assertIn("has_edge", d)
        self.assertEqual(d["has_edge"], e.has_edge)
        self.assertEqual(d["outcome"], "Home")

    def test_fair_price_is_the_fair_prob_as_american(self):
        e = odds.find_edges(self.OUTCOMES, self.FEED, ["fanduel"],
                            sharp_book="pinnacle")[0]
        self.assertEqual(e.fair_price, odds.implied_to_american(e.fair_prob))

    def test_no_free_money_against_the_sharp_book_itself(self):
        # Betting at the book that defines fair can only lose its vig.
        e = odds.find_edges(self.OUTCOMES, self.FEED, ["pinnacle"],
                            sharp_book="pinnacle")
        self.assertFalse(any(x.has_edge for x in e))


class TestConsensus(unittest.TestCase):

    FEED = {"pinnacle": [-150, 135], "fanduel": [-140, 120], "draftkings": [-155, 130]}

    def test_consensus_normalises(self):
        self.assertAlmostEqual(sum(odds.consensus_fair_probs(self.FEED)), 1.0, places=12)

    def test_consensus_of_one_book_is_that_book_devigged(self):
        want = odds.devig(self.FEED["pinnacle"], "power")
        got = odds.consensus_fair_probs({"pinnacle": self.FEED["pinnacle"]})
        for w, g in zip(want, got):
            self.assertAlmostEqual(w, g, places=12)

    def test_exclusion_changes_the_answer(self):
        allbooks = odds.consensus_fair_probs(self.FEED)
        without = odds.consensus_fair_probs(self.FEED, exclude="fanduel")
        self.assertNotAlmostEqual(allbooks[0], without[0], places=6)

    def test_empty_feed_raises(self):
        with self.assertRaises(ValueError):
            odds.consensus_fair_probs({})

    def test_excluding_the_only_book_raises(self):
        with self.assertRaises(ValueError):
            odds.consensus_fair_probs({"fanduel": [-110, -110]}, exclude="fanduel")

    def test_ragged_markets_raise(self):
        with self.assertRaises(ValueError):
            odds.consensus_fair_probs({"a": [-110, -110], "b": [-110, 200, 400]})

    def test_books_with_no_prices_are_ignored(self):
        want = odds.consensus_fair_probs(self.FEED)
        got = odds.consensus_fair_probs(dict(self.FEED, offline=[]))
        for w, g in zip(want, got):
            self.assertAlmostEqual(w, g, places=12)


class TestParlayReport(unittest.TestCase):

    def test_empty(self):
        self.assertEqual(odds.parlay_report([]), {"legs": 0})

    def test_prices_and_payout(self):
        legs = [{"label": "a", "american": -110}, {"label": "b", "american": -110}]
        r = odds.parlay_report(legs, stake=100.0)
        self.assertEqual(r["legs"], 2)
        self.assertEqual(r["american"], 264)
        self.assertAlmostEqual(r["decimal"], 3.6446, places=4)
        self.assertAlmostEqual(r["to_return"], 364.46, places=2)
        self.assertAlmostEqual(r["profit"], 264.46, places=2)

    def test_breakeven_is_the_inverse_decimal(self):
        legs = [{"american": -110}, {"american": -110}]
        r = odds.parlay_report(legs)
        self.assertAlmostEqual(r["breakeven_prob"], 1.0 / 3.6446281, places=6)
        self.assertEqual(r["breakeven_prob"], r["book_implied_prob"])

    def test_missing_fair_prob_leaves_ev_none_with_a_note(self):
        legs = [{"american": -110, "fair_prob": 0.55}, {"american": -110}]
        r = odds.parlay_report(legs)
        self.assertIsNone(r["ev_dollars"])
        self.assertIsNone(r["fair_prob"])
        self.assertIsNone(r["total_hold"])
        self.assertTrue(any("no fair-price estimate" in n for n in r["notes"]))

    def test_fair_prob_is_the_product_of_legs(self):
        legs = [{"american": -110, "fair_prob": 0.55},
                {"american": -110, "fair_prob": 0.60}]
        r = odds.parlay_report(legs)
        self.assertAlmostEqual(r["fair_prob"], 0.33, places=6)

    def test_positive_ev_legs_make_a_positive_ev_parlay(self):
        legs = [{"american": -110, "fair_prob": 0.55},
                {"american": -110, "fair_prob": 0.55}]
        r = odds.parlay_report(legs, stake=100.0)
        self.assertGreater(r["ev_dollars"], 0.0)

    def test_total_hold_equals_compounded_per_leg_hold(self):
        # Two fair-50% legs priced at -110 each. Per leg the book holds
        # 4.545%; across two independent legs it keeps
        #   1 - 0.95455^2 = 0.088843
        # of the stake. Anything negative here means the ratio is inverted:
        # the book does not give money away on a -110/-110 parlay.
        legs = [{"american": -110, "fair_prob": 0.5},
                {"american": -110, "fair_prob": 0.5}]
        r = odds.parlay_report(legs)
        per_leg = odds.hold([-110, -110])
        self.assertAlmostEqual(r["total_hold"], 1.0 - (1.0 - per_leg) ** 2, places=6)
        self.assertAlmostEqual(r["total_hold"], 0.088843, places=6)
        self.assertGreater(r["total_hold"], 0.0)

    def test_total_hold_is_zero_on_a_fair_parlay(self):
        legs = [{"american": 100, "fair_prob": 0.5},
                {"american": 100, "fair_prob": 0.5}]
        r = odds.parlay_report(legs)
        self.assertAlmostEqual(r["total_hold"], 0.0, places=9)
        self.assertAlmostEqual(r["ev_dollars"], 0.0, places=6)

    def test_total_hold_grows_with_legs(self):
        def h(n):
            legs = [{"american": -110, "fair_prob": 0.5}] * n
            return odds.parlay_report(legs)["total_hold"]
        self.assertLess(h(2), h(3))
        self.assertLess(h(3), h(6))

    def test_same_game_is_flagged_and_noted(self):
        legs = [{"american": -110, "game_id": "G1"},
                {"american": 140, "game_id": "G1"}]
        r = odds.parlay_report(legs)
        self.assertTrue(r["same_game"])
        self.assertTrue(any("correlated" in n for n in r["notes"]))

    def test_different_games_not_flagged(self):
        legs = [{"american": -110, "game_id": "G1"},
                {"american": 140, "game_id": "G2"}]
        self.assertFalse(odds.parlay_report(legs)["same_game"])

    def test_partially_tagged_legs_are_not_called_same_game(self):
        # Only one leg carries a game_id, so we do not know the others are
        # in that game and must not claim correlation.
        legs = [{"american": -110, "game_id": "G1"},
                {"american": 140},
                {"american": 200}]
        self.assertFalse(odds.parlay_report(legs)["same_game"])

    def test_untagged_legs_are_not_flagged(self):
        legs = [{"american": -110}, {"american": 140}]
        self.assertFalse(odds.parlay_report(legs)["same_game"])

    def test_single_leg_is_never_same_game(self):
        self.assertFalse(odds.parlay_report([{"american": -110, "game_id": "G1"}])["same_game"])

    def test_four_legs_get_the_compounding_note(self):
        legs = [{"american": -110}] * 4
        r = odds.parlay_report(legs)
        self.assertTrue(any("Hold compounds" in n for n in r["notes"]))

    def test_independence_is_always_declared(self):
        self.assertTrue(odds.parlay_report([{"american": -110}])["independence_assumed"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
