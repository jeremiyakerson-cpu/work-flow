"""
Odds math. No dependencies, fully unit-tested.

Everything here is deterministic arithmetic - no opinions, no models.
The one judgement call is which de-vig method you trust; see devig().
"""
from __future__ import annotations

from dataclasses import dataclass, asdict
from typing import Iterable, Sequence


# ---------- format conversion ----------

def american_to_decimal(american: float) -> float:
    if american > 0:
        return 1.0 + american / 100.0
    return 1.0 + 100.0 / abs(american)


def decimal_to_american(decimal: float) -> int:
    if decimal >= 2.0:
        return round((decimal - 1.0) * 100)
    return round(-100.0 / (decimal - 1.0))


def american_to_implied(american: float) -> float:
    """Raw implied probability, vig included. Sums to >1 across a market."""
    if american > 0:
        return 100.0 / (american + 100.0)
    return abs(american) / (abs(american) + 100.0)


def implied_to_american(p: float) -> int:
    if not 0.0 < p < 1.0:
        raise ValueError("probability must be strictly between 0 and 1")
    return decimal_to_american(1.0 / p)


# ---------- vig ----------

def overround(americans: Sequence[float]) -> float:
    """
    Sum of raw implied probabilities minus 1.

    A -110/-110 market gives 0.0476. This is NOT what the industry calls
    hold - it is the excess over a fair book. Easy to confuse; keep them
    separate.
    """
    return sum(american_to_implied(a) for a in americans) - 1.0


def hold(americans: Sequence[float]) -> float:
    """
    Theoretical hold: the book's expected margin as a share of total handle.

        hold = 1 - 1 / sum(implied probabilities)

    A -110/-110 market holds 4.545%, which is the number books and
    industry sources quote. Dividing by the overround matters: using
    overround instead overstates the margin on every market.
    """
    return 1.0 - 1.0 / sum(american_to_implied(a) for a in americans)


def devig_multiplicative(americans: Sequence[float]) -> list[float]:
    """Proportional de-vig. Simple and standard; slightly favours the favourite."""
    raw = [american_to_implied(a) for a in americans]
    total = sum(raw)
    return [r / total for r in raw]


def devig_additive(americans: Sequence[float]) -> list[float]:
    """Subtract the vig evenly across outcomes."""
    raw = [american_to_implied(a) for a in americans]
    excess = (sum(raw) - 1.0) / len(raw)
    return [r - excess for r in raw]


def devig_power(americans: Sequence[float], tol: float = 1e-12) -> list[float]:
    """
    Power de-vig: solve for k where sum(p_i ** k) == 1.

    Generally the best-behaved simple method on lopsided markets, which is
    where multiplicative de-vig misleads you most - heavy favourites and
    longshot props.
    """
    raw = [american_to_implied(a) for a in americans]
    lo, hi = 0.2, 5.0
    k = 1.0
    for _ in range(300):
        k = (lo + hi) / 2.0
        s = sum(r ** k for r in raw)
        if abs(s - 1.0) < tol:
            break
        if s > 1.0:
            lo = k
        else:
            hi = k
    return [r ** k for r in raw]


DEVIG_METHODS = {
    "multiplicative": devig_multiplicative,
    "additive": devig_additive,
    "power": devig_power,
}


def devig(americans: Sequence[float], method: str = "power") -> list[float]:
    if method not in DEVIG_METHODS:
        raise ValueError(f"unknown de-vig method: {method}")
    return DEVIG_METHODS[method](americans)


# ---------- parlays ----------

def parlay_decimal(americans: Iterable[float]) -> float:
    d = 1.0
    for a in americans:
        d *= american_to_decimal(a)
    return d


def parlay_american(americans: Iterable[float]) -> int:
    return decimal_to_american(parlay_decimal(americans))


# ---------- value ----------

def expected_value(stake: float, american: float, true_prob: float) -> float:
    """Dollar EV of one wager at a given true probability."""
    dec = american_to_decimal(american)
    profit = stake * (dec - 1.0)
    return true_prob * profit - (1.0 - true_prob) * stake


def ev_percent(american: float, true_prob: float) -> float:
    """EV as a fraction of stake. 0.03 means +3 cents per dollar risked."""
    return expected_value(1.0, american, true_prob)


def kelly_fraction(american: float, true_prob: float) -> float:
    """
    Full-Kelly fraction of bankroll; 0 when there is no edge.

    Use a fraction of this in practice. Full Kelly is only correct if your
    probability estimate is exactly right, which it never is. Quarter Kelly
    is the common compromise.
    """
    b = american_to_decimal(american) - 1.0
    q = 1.0 - true_prob
    f = (b * true_prob - q) / b
    return max(0.0, f)


def breakeven_prob(american: float) -> float:
    """Win rate needed just to break even at this price."""
    return american_to_implied(american)


# ---------- comparison ----------

@dataclass
class Edge:
    outcome: str
    your_book: str
    your_price: int           # price at a book you can actually bet
    best_book: str
    best_price: int           # best price anywhere in the feed
    fair_prob: float          # no-vig fair probability
    fair_price: int
    fair_source: str          # "sharp:<book>" or "consensus"
    ev_per_dollar: float      # at your_price
    ev_dollars: float         # at your_price and the stake passed in
    kelly: float
    hold_at_your_book: float

    @property
    def has_edge(self) -> bool:
        return self.ev_per_dollar > 0.0

    def to_dict(self) -> dict:
        d = asdict(self)
        d["has_edge"] = self.has_edge
        return d


def consensus_fair_probs(
    book_markets: dict[str, list[float]],
    method: str = "power",
    exclude: str | None = None,
) -> list[float]:
    """
    Average de-vigged probabilities across books.

    book_markets: {"pinnacle": [-150, 130], "fanduel": [-155, 132], ...}
                  every list is the same outcomes in the same order.
    exclude:      drop a book from the consensus. Use this to keep the book
                  you are betting at out of its own fairness check.
    """
    rows = [
        devig(prices, method)
        for book, prices in book_markets.items()
        if book != exclude and prices
    ]
    if not rows:
        raise ValueError("no books left to build a consensus from")
    n = len(rows[0])
    if any(len(r) != n for r in rows):
        raise ValueError("all books must quote the same number of outcomes")
    avg = [sum(r[i] for r in rows) / len(rows) for i in range(n)]
    total = sum(avg)
    return [a / total for a in avg]


def find_edges(
    outcomes: Sequence[str],
    book_markets: dict[str, list[float]],
    your_books: Sequence[str],
    stake: float = 10.0,
    method: str = "power",
    sharp_book: str | None = None,
) -> list[Edge]:
    """
    Compare what you can actually bet against the market's fair price.

    If sharp_book is in the feed, its de-vigged line alone is the fair
    estimate - a sharp book's number carries more information than an
    average of soft books. Otherwise fall back to a consensus that excludes
    your own book, so it is not grading its own homework.
    """
    edges: list[Edge] = []
    for your_book in your_books:
        mine = book_markets.get(your_book)
        if not mine:
            continue
        if sharp_book and sharp_book in book_markets and book_markets[sharp_book]:
            fair = devig(book_markets[sharp_book], method)
            source = f"sharp:{sharp_book}"
        else:
            fair = consensus_fair_probs(book_markets, method, exclude=your_book)
            source = "consensus"
        my_hold = hold(mine)
        for i, name in enumerate(outcomes):
            best_book, best_price = max(
                ((b, p[i]) for b, p in book_markets.items() if p),
                key=lambda bp: american_to_decimal(bp[1]),
            )
            edges.append(Edge(
                outcome=name,
                your_book=your_book,
                your_price=int(mine[i]),
                best_book=best_book,
                best_price=int(best_price),
                fair_prob=fair[i],
                fair_price=implied_to_american(fair[i]),
                fair_source=source,
                ev_per_dollar=ev_percent(mine[i], fair[i]),
                ev_dollars=expected_value(stake, mine[i], fair[i]),
                kelly=kelly_fraction(mine[i], fair[i]),
                hold_at_your_book=my_hold,
            ))
    return edges


def parlay_report(legs: Sequence[dict], stake: float = 10.0) -> dict:
    """
    legs: [{"label": ..., "american": -110, "fair_prob": 0.53}, ...]

    fair_prob may be None for a leg with no fair estimate. When any leg is
    missing one, EV is left as None rather than guessed.

    Independence is assumed. Same-game legs are correlated and this will
    overstate the true probability for them; same_game flags that.
    """
    if not legs:
        return {"legs": 0}

    prices = [l["american"] for l in legs]
    dec = parlay_decimal(prices)
    am = decimal_to_american(dec)
    book_prob = 1.0 / dec

    fairs = [l.get("fair_prob") for l in legs]
    complete = len(fairs) > 0 and all(f is not None for f in fairs)

    game_ids = {l.get("game_id") for l in legs if l.get("game_id")}
    same_game = len(legs) > 1 and len(game_ids) == 1

    out = {
        "legs": len(legs),
        "american": am,
        "decimal": round(dec, 4),
        "stake": stake,
        "to_return": round(stake * dec, 2),
        "profit": round(stake * (dec - 1.0), 2),
        "book_implied_prob": round(book_prob, 6),
        "breakeven_prob": round(book_prob, 6),
        "fair_prob": None,
        "ev_dollars": None,
        "ev_per_dollar": None,
        "total_hold": None,
        "independence_assumed": True,
        "same_game": same_game,
        "notes": [],
    }

    if complete:
        fair_prob = 1.0
        for f in fairs:
            fair_prob *= f
        out["fair_prob"] = round(fair_prob, 6)
        out["ev_dollars"] = round(expected_value(stake, am, fair_prob), 2)
        out["ev_per_dollar"] = round(ev_percent(am, fair_prob), 6)
        out["total_hold"] = round(1.0 - book_prob / fair_prob, 6)
    else:
        out["notes"].append(
            "EV not computed: at least one leg has no fair-price estimate."
        )

    if same_game:
        out["notes"].append(
            "All legs are in one game. Legs in the same game are correlated, "
            "so the independent calculation above is not the true probability. "
            "Treat the EV as indicative only."
        )
    if len(legs) >= 4:
        out["notes"].append(
            f"{len(legs)} legs. Hold compounds with every leg added."
        )
    return out
