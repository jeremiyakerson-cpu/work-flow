import math
import sys, os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from app.math_engine import (
    american_to_decimal, decimal_to_american, american_to_implied,
    implied_to_american, hold, overround, devig, devig_power, devig_multiplicative,
    parlay_american, parlay_decimal, expected_value, ev_percent,
    kelly_fraction, breakeven_prob, consensus_fair_probs, find_edges,
    parlay_report,
)

ok = 0
fail = []

def check(name, cond):
    global ok
    if cond:
        ok += 1
    else:
        fail.append(name)

def close(a, b, tol=1e-6):
    return abs(a - b) < tol

# conversions
check("-110 -> 1.909", close(american_to_decimal(-110), 1.9090909, 1e-6))
check("+150 -> 2.5", close(american_to_decimal(150), 2.5))
check("2.5 -> +150", decimal_to_american(2.5) == 150)
check("1.909 -> -110", decimal_to_american(1.9090909) == -110)
check("-110 implied", close(american_to_implied(-110), 0.5238095, 1e-6))
check("+150 implied", close(american_to_implied(150), 0.4))
check("roundtrip implied", implied_to_american(0.4) == 150)

# hold: standard -110/-110 is 4.545%
check("hold -110/-110", close(hold([-110, -110]), 0.0454545, 1e-6))
check("overround -110/-110", close(overround([-110, -110]), 0.047619, 1e-6))
check("hold ATH/TB", close(hold([190, -230]), 0.0401, 1e-3))

# de-vig sums to 1
for m in ("multiplicative", "additive", "power"):
    p = devig([-230, 190], m)
    check(f"devig {m} sums to 1", close(sum(p), 1.0, 1e-9))
    check(f"devig {m} ordered", p[0] > p[1])

# power de-vig on a lopsided market stays sane
p = devig_power([-2000, 1100])
check("power lopsided sums 1", close(sum(p), 1.0, 1e-9))
check("power lopsided fav high", 0.90 < p[0] < 0.99)

# multiplicative vs power differ on lopsided markets
pm = devig_multiplicative([-2000, 1100])
check("power != multiplicative on longshots", abs(pm[0] - p[0]) > 1e-4)

# parlays
check("two -110 legs = +264", parlay_american([-110, -110]) == 264)
check("parlay decimal", close(parlay_decimal([100, 100]), 4.0))
check("three +100 legs = +700", parlay_american([100, 100, 100]) == 700)

# EV
check("EV fair coin at +100 is 0", close(expected_value(10, 100, 0.5), 0.0))
check("EV -110 at 52.38% is 0", close(expected_value(10, -110, 0.5238095), 0.0, 1e-4))
check("EV positive when edge", expected_value(10, 100, 0.55) > 0)
check("EV negative when no edge", expected_value(10, -110, 0.50) < 0)
check("ev_percent scale", close(ev_percent(100, 0.55), 0.10, 1e-9))

# Kelly
check("kelly no edge = 0", kelly_fraction(-110, 0.50) == 0.0)
check("kelly 55% at +100 = 10%", close(kelly_fraction(100, 0.55), 0.10, 1e-9))
check("breakeven -110", close(breakeven_prob(-110), 0.5238095, 1e-6))

# consensus
books = {
    "pinnacle": [-150, 135],
    "fanduel":  [-155, 132],
    "draftkings": [-152, 134],
}
c = consensus_fair_probs(books)
check("consensus sums 1", close(sum(c), 1.0, 1e-9))
check("consensus excludes", not close(
    consensus_fair_probs(books, exclude="fanduel")[0], c[0], 1e-9))

# edges: FD offering a worse price than the sharp fair should be -EV
edges = find_edges(["Home", "Away"], books, ["fanduel", "draftkings"],
                   stake=10, sharp_book="pinnacle")
check("edges count", len(edges) == 4)
fd_home = [e for e in edges if e.your_book == "fanduel" and e.outcome == "Home"][0]
check("fd home uses sharp", fd_home.fair_source == "sharp:pinnacle")
check("fd home -EV vs pinnacle", fd_home.ev_per_dollar < 0)
check("best price found", fd_home.best_price == -150 and fd_home.best_book == "pinnacle")

# a genuinely good price should show +EV
good = {"pinnacle": [-150, 135], "fanduel": [-135, 150]}
e2 = find_edges(["Home", "Away"], good, ["fanduel"], sharp_book="pinnacle")
check("plus EV detected", any(e.has_edge for e in e2))

# parlay report
rep = parlay_report([
    {"label": "A", "american": -110, "fair_prob": 0.55, "game_id": "g1"},
    {"label": "B", "american": -110, "fair_prob": 0.55, "game_id": "g2"},
], stake=10)
check("parlay price", rep["american"] == 264)
check("parlay fair prob", close(rep["fair_prob"], 0.3025, 1e-6))
check("parlay not same game", rep["same_game"] is False)
check("parlay EV computed", rep["ev_dollars"] is not None)

same = parlay_report([
    {"label": "A", "american": -110, "fair_prob": 0.55, "game_id": "g1"},
    {"label": "B", "american": -110, "fair_prob": 0.55, "game_id": "g1"},
])
check("same game flagged", same["same_game"] is True)
check("same game note", any("correlated" in n for n in same["notes"]))

missing = parlay_report([
    {"label": "A", "american": -110, "fair_prob": 0.55},
    {"label": "B", "american": 300, "fair_prob": None},
])
check("missing fair -> no EV", missing["ev_dollars"] is None)
check("missing fair noted", any("not computed" in n for n in missing["notes"]))

# real numbers from the screenshots: ATH/TB moneyline
p_tb = devig([190, -230])[1]
check("TB fair ~67.7%", 0.66 < p_tb < 0.69)

print(f"{ok} passed, {len(fail)} failed")
for f in fail:
    print("  FAIL:", f)
sys.exit(1 if fail else 0)
