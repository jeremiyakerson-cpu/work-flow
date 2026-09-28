"""
Fair-price math: how the desk turns many books' prices into one fair
probability, and what each graded price is compared against.

The rule under test: a book is never graded against a consensus that
contains its own price. Run: python3 -m pytest tests/
"""
import pytest

from app.math_engine import (
    consensus_fair_probs, devig, ev_percent,
)
from app.services.board import analyse_market, fair_estimate, build_board
from app.services.props import analyse_event_markets

LABELS = ["home", "away"]


def _yours(res, side, book):
    return next(y for y in res["sides"][side]["your_books"] if y["book"] == book)


# ---------------- consensus math ----------------

def test_consensus_is_the_mean_of_each_books_devig():
    books = {"a": [-150, 130], "b": [-140, 120], "c": [-160, 135]}
    rows = [devig(p, "power") for p in books.values()]
    want = [sum(r[i] for r in rows) / 3 for i in range(2)]
    got = consensus_fair_probs(books, "power")
    assert got == pytest.approx(want, abs=1e-12)
    assert sum(got) == pytest.approx(1.0, abs=1e-12)


def test_identical_balanced_books_are_a_coin_flip():
    books = {b: [-110, -110] for b in "abcd"}
    for m in ("power", "multiplicative", "additive"):
        assert consensus_fair_probs(books, m) == pytest.approx([0.5, 0.5])


def test_three_way_consensus_sums_to_one_and_keeps_order():
    books = {"a": [150, 240, 210], "b": [145, 250, 205], "c": [155, 235, 215]}
    fair = consensus_fair_probs(books, "power")
    assert sum(fair) == pytest.approx(1.0)
    # the shortest price is the favourite at every book
    assert fair[0] == max(fair)


# ---------------- leave-one-out grading ----------------

def test_graded_book_is_left_out_of_its_own_consensus():
    books = {
        "fanduel": [-190, 165],     # the overlay being graded
        "b1": [-165, 140], "b2": [-160, 135], "b3": [-170, 145],
    }
    res = analyse_market(books, LABELS, ["fanduel"], stake=100)
    fd = _yours(res, 1, "fanduel")

    others = {k: v for k, v in books.items() if k != "fanduel"}
    fair_wo = consensus_fair_probs(others, "power")[1]
    assert fd["fair_prob"] == pytest.approx(fair_wo, abs=1e-5)
    assert fd["ev_per_dollar"] == pytest.approx(ev_percent(165, fair_wo), abs=1e-5)
    assert fd["ev_per_dollar"] > 0
    assert fd["fair_source"] == "consensus:3 books (excl. fanduel)"

    # the old behaviour: fanduel inside its own consensus drags fair toward
    # its price and hides part of the edge
    fair_with = consensus_fair_probs(books, "power")[1]
    assert ev_percent(165, fair_with) < fd["ev_per_dollar"]

    # the market's displayed fair price still uses every book
    assert res["sides"][1]["fair_prob"] == pytest.approx(fair_with, abs=1e-5)
    assert res["fair_source"] == "consensus:4 books"


def test_each_of_your_books_gets_its_own_fair():
    books = {"fanduel": [-150, 150], "draftkings": [-170, 150],
             "b1": [-160, 140], "b2": [-160, 140]}
    res = analyse_market(books, LABELS, ["fanduel", "draftkings"], stake=10)
    fd, dk = _yours(res, 0, "fanduel"), _yours(res, 0, "draftkings")
    assert fd["fair_prob"] != dk["fair_prob"]
    assert "excl. fanduel" in fd["fair_source"] and "excl. draftkings" in dk["fair_source"]


def test_best_price_is_graded_without_the_best_book():
    books = {"x": [-150, 160], "b1": [-165, 140], "b2": [-160, 135], "b3": [-170, 145]}
    res = analyse_market(books, LABELS, [], stake=10)
    away = res["sides"][1]
    assert away["best_book"] == "x"
    fair_wo = consensus_fair_probs({k: v for k, v in books.items() if k != "x"}, "power")[1]
    assert away["best_ev_per_dollar"] == pytest.approx(ev_percent(160, fair_wo), abs=1e-5)


def test_two_books_grades_against_the_other_one():
    books = {"fanduel": [-120, 100], "other": [-130, 110]}
    fd = _yours(analyse_market(books, LABELS, ["fanduel"], stake=10), 0, "fanduel")
    assert fd["fair_source"] == "single book:other (excl. fanduel)"
    assert fd["fair_prob"] == pytest.approx(devig([-130, 110])[0], abs=1e-5)


def test_only_your_book_quoting_means_no_ev_not_self_grading():
    res = analyse_market({"fanduel": [-120, 100]}, LABELS, ["fanduel"], stake=10)
    assert res["fair_source"] == "single book:fanduel"       # still shown, labelled weak
    fd = _yours(res, 0, "fanduel")
    assert fd["ev_per_dollar"] is None and fd["fair_prob"] is None and fd["kelly"] is None
    assert fd["hold"] is not None


def test_sharp_book_is_the_fair_unless_it_is_being_graded():
    books = {"pinnacle": [-145, 135], "fanduel": [-150, 130],
             "b1": [-160, 140], "b2": [-155, 135]}
    res = analyse_market(books, LABELS, ["fanduel", "pinnacle"], stake=10, sharp_book="pinnacle")
    assert res["fair_source"] == "sharp:pinnacle"
    assert _yours(res, 0, "fanduel")["fair_source"] == "sharp:pinnacle (excl. fanduel)"
    assert _yours(res, 0, "fanduel")["fair_prob"] == pytest.approx(devig([-145, 135])[0], abs=1e-5)
    # grading the sharp itself: fall back to everyone else
    pin = _yours(res, 0, "pinnacle")
    assert pin["fair_source"] == "consensus:3 books (excl. pinnacle)"


def test_impossible_additive_devig_prices_nothing():
    # heavy vig spread evenly pushes a long shot below zero
    books = {"a": [-200, -110, 5000]}
    assert devig(books["a"], "additive")[2] < 0
    assert fair_estimate(books, "additive") is None
    assert analyse_market(books, ["h", "d", "a"], [], 10, method="additive") is None


def test_board_plays_carry_the_fair_they_were_graded_against():
    odds = [{
        "odds_id": "g1", "start_utc": "2026-10-01T00:00:00Z", "home": "H", "away": "A",
        "books": {
            "fanduel": {"h2h": [{"name": "H", "price": -190}, {"name": "A", "price": 165}]},
            "b1": {"h2h": [{"name": "H", "price": -165}, {"name": "A", "price": 140}]},
            "b2": {"h2h": [{"name": "H", "price": -160}, {"name": "A", "price": 135}]},
            "b3": {"h2h": [{"name": "H", "price": -170}, {"name": "A", "price": 145}]},
        }}]
    b = build_board([], odds, your_books=("fanduel",), markets=("h2h",))
    play = next(p for p in b["plays"] if p["side"] == "A")
    assert play["fair_source"] == "consensus:3 books (excl. fanduel)"
    fair_wo = consensus_fair_probs({"b1": [-165, 140], "b2": [-160, 135], "b3": [-170, 145]})[1]
    assert play["fair_prob"] == pytest.approx(fair_wo, abs=1e-5)
    assert play["ev_per_dollar"] == pytest.approx(ev_percent(165, fair_wo), abs=1e-5)


# ---------------- props: multi-way and one-sided ----------------

def _event(bookmakers):
    return {"id": "e1", "home_team": "Home FC", "away_team": "Away FC",
            "commence_time": "2026-10-01T00:00:00Z", "bookmakers": bookmakers}


def _bm(key, market, outcomes):
    return {"key": key, "markets": [{"key": market, "outcomes": outcomes}]}


def test_three_way_market_is_devigged_as_one_market():
    prices = {"a": (150, 240, 210), "b": (145, 250, 205), "c": (155, 235, 215)}
    ev = _event([_bm(k, "h2h_3_way", [
        {"name": "Home FC", "price": h}, {"name": "Draw", "price": d},
        {"name": "Away FC", "price": a}]) for k, (h, d, a) in prices.items()])
    res = analyse_event_markets(ev, ["a"])
    assert len(res["props"]) == 1
    row = res["props"][0]
    assert len(row["labels"]) == 3 and "Draw" in row["labels"]
    sides = row["analysis"]["sides"]
    assert sum(s["fair_prob"] for s in sides) == pytest.approx(1.0, abs=1e-4)
    order = [row["labels"].index(n) for n in ("Home FC", "Draw", "Away FC")]
    want = consensus_fair_probs({k: list(v) for k, v in prices.items()}, "power")
    for i, w in zip(order, want):
        assert sides[i]["fair_prob"] == pytest.approx(w, abs=1e-5)
    # book "a" is graded against b and c only
    a_home = _yours(row["analysis"], order[0], "a")
    loo = consensus_fair_probs({k: list(v) for k, v in prices.items() if k != "a"}, "power")
    assert a_home["fair_prob"] == pytest.approx(loo[0], abs=1e-5)


def test_one_sided_quote_is_graded_against_the_two_sided_books():
    ou = lambda o, u: [{"name": "Over", "description": "P", "point": 24.5, "price": o},
                       {"name": "Under", "description": "P", "point": 24.5, "price": u}]
    ev = _event([
        _bm("fanduel", "player_points",
            [{"name": "Over", "description": "P", "point": 24.5, "price": 130}]),
        _bm("b1", "player_points", ou(-115, -105)),
        _bm("b2", "player_points", ou(-110, -110)),
        _bm("b3", "player_points", ou(-120, 100)),
    ])
    res = analyse_event_markets(ev, ["fanduel"])
    row = res["props"][0]
    a = row["analysis"]
    assert a["book_count"] == 3                 # fanduel is not in the consensus
    fd = _yours(a, 0, "fanduel")
    fair = consensus_fair_probs({"b1": [-115, -105], "b2": [-110, -110], "b3": [-120, 100]})[0]
    assert fd["one_sided"] is True and fd["hold"] is None
    assert fd["fair_prob"] == pytest.approx(fair, abs=1e-5)
    assert fd["ev_per_dollar"] == pytest.approx(ev_percent(130, fair), abs=1e-5)
    play = res["plays"][0]
    assert play["book"] == "fanduel" and play["one_sided"] is True


def test_market_quoted_on_one_side_everywhere_has_no_fair():
    ev = _event([_bm(k, "player_anytime_td",
                     [{"name": "Yes", "description": "P", "price": p}])
                 for k, p in (("fanduel", 120), ("b1", 110), ("b2", 115))])
    row = analyse_event_markets(ev, ["fanduel"])["props"][0]
    assert row["analysis"] is None and "one side" in row["note"]
