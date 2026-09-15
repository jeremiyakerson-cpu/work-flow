"""
The desk layer: feed in, ranked edges out.

This module holds no math of its own. It picks the fair-price benchmark per
market, calls odds.find_edges, and adds staking on top of the Kelly fraction
the math returns.
"""
from __future__ import annotations

from typing import Any

from . import odds
from .config import Settings


def _sharp_for(market: dict[str, Any], settings: Settings) -> str | None:
    """The sharp book, but only if it actually quoted this market."""
    sharp = settings.sharp_book
    if sharp and market["books"].get(sharp):
        return sharp
    return None


def analyze_market(
    market: dict[str, Any],
    settings: Settings,
    stake: float | None = None,
    method: str | None = None,
) -> list[dict[str, Any]]:
    stake = settings.default_stake if stake is None else stake
    method = method or settings.devig_method

    bettable = [b for b in settings.your_books if market["books"].get(b)]
    if not bettable:
        return []

    edges = odds.find_edges(
        outcomes=market["outcomes"],
        book_markets=market["books"],
        your_books=bettable,
        stake=stake,
        method=method,
        sharp_book=_sharp_for(market, settings),
    )

    rows = []
    for e in edges:
        row = e.to_dict()
        row.update({
            "market_id": market["id"],
            "market_label": market["label"],
            "sport": market["sport"],
            "game_id": market["game_id"],
            "commence": market["commence"],
            # Fractional Kelly. Full Kelly assumes the fair estimate is
            # exactly right, so the desk never stakes it.
            "kelly_stake": round(
                settings.bankroll * e.kelly * settings.kelly_multiplier, 2
            ),
            "kelly_multiplier": settings.kelly_multiplier,
            # A better price exists elsewhere; the edge shown is at YOUR price.
            "beat_by_best": e.best_price != e.your_price,
        })
        rows.append(row)
    return rows


def analyze_all(
    markets: list[dict[str, Any]],
    settings: Settings,
    stake: float | None = None,
    method: str | None = None,
    min_ev: float | None = None,
) -> dict[str, Any]:
    min_ev = settings.min_ev if min_ev is None else min_ev
    method = method or settings.devig_method

    rows: list[dict[str, Any]] = []
    skipped: list[dict[str, str]] = []
    for m in markets:
        got = analyze_market(m, settings, stake=stake, method=method)
        if not got:
            skipped.append({
                "market_id": m["id"],
                "reason": "none of YOUR_BOOKS quote this market",
            })
        rows.extend(got)

    priced = [r for r in rows if r["ev_per_dollar"] >= min_ev]
    priced.sort(key=lambda r: r["ev_per_dollar"], reverse=True)

    consensus_only = sorted({
        r["market_id"] for r in rows if r["fair_source"] == "consensus"
    })

    return {
        "rows": priced,
        "total_rows": len(rows),
        "shown": len(priced),
        "positive_ev": sum(1 for r in rows if r["has_edge"]),
        "method": method,
        "min_ev": min_ev,
        "stake": settings.default_stake if stake is None else stake,
        "sharp_book": settings.sharp_book,
        "your_books": settings.your_books,
        "bankroll": settings.bankroll,
        "kelly_multiplier": settings.kelly_multiplier,
        "consensus_only_markets": consensus_only,
        "skipped": skipped,
    }


def build_parlay(
    legs: list[dict[str, Any]],
    markets: list[dict[str, Any]],
    settings: Settings,
    stake: float | None = None,
    method: str | None = None,
) -> dict[str, Any]:
    """
    legs: [{"market_id": ..., "outcome_index": 0, "book": "fanduel"}, ...]

    Each leg is priced at the named book and graded against that market's
    fair estimate. A leg whose market has no fair benchmark comes back with
    fair_prob None, which makes parlay_report withhold EV rather than guess.
    """
    stake = settings.default_stake if stake is None else stake
    method = method or settings.devig_method
    by_id = {m["id"]: m for m in markets}

    resolved: list[dict[str, Any]] = []
    for leg in legs:
        m = by_id.get(leg.get("market_id"))
        if m is None:
            raise ValueError(f"unknown market_id: {leg.get('market_id')!r}")
        i = int(leg.get("outcome_index", 0))
        if not 0 <= i < len(m["outcomes"]):
            raise ValueError(f"outcome_index {i} out of range for {m['id']}")
        book = leg.get("book") or next(
            (b for b in settings.your_books if m["books"].get(b)), None
        )
        prices = m["books"].get(book)
        if not prices:
            raise ValueError(f"book {book!r} does not quote {m['id']}")

        sharp = _sharp_for(m, settings)
        if sharp:
            fair = odds.devig(m["books"][sharp], method)[i]
        else:
            try:
                fair = odds.consensus_fair_probs(
                    m["books"], method, exclude=book
                )[i]
            except ValueError:
                fair = None

        resolved.append({
            "label": f"{m['label']} - {m['outcomes'][i]} @ {book}",
            "american": prices[i],
            "fair_prob": fair,
            "game_id": m["game_id"],
            "market_id": m["id"],
            "book": book,
            "outcome": m["outcomes"][i],
        })

    report = odds.parlay_report(resolved, stake=stake)
    report["legs_detail"] = resolved

    # The same outcome staked twice is not two independent legs: its true
    # joint probability is p, not p*p, so the report understates fair_prob
    # and overstates the edge. parlay_report's same_game flag is too weak
    # to convey this, so name it outright.
    seen: dict[tuple[str, str], int] = {}
    for leg in resolved:
        key = (leg["market_id"], leg["outcome"])
        seen[key] = seen.get(key, 0) + 1
    dupes = [f"{o} ({n}x)" for (_, o), n in seen.items() if n > 1]
    report["duplicate_outcomes"] = dupes
    if dupes:
        report.setdefault("notes", []).append(
            "The same outcome appears more than once: "
            + ", ".join(dupes)
            + ". Repeating an outcome does not compound - the true joint "
            "probability is the leg's own probability, so the fair price and "
            "EV shown here are wrong, not merely optimistic."
        )
    return report
