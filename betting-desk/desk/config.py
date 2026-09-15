"""
Configuration. Reads .env then the real environment; no dependencies.

Real environment variables win over .env, so `PORT=9000 ./run.sh` works.
"""
from __future__ import annotations

import os
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def load_dotenv(path: Path | None = None) -> dict[str, str]:
    """Minimal .env parser: KEY=value, # comments, optional quotes."""
    path = path or ROOT / ".env"
    out: dict[str, str] = {}
    if not path.exists():
        return out
    for line in path.read_text().splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        key = key.strip()
        value = value.strip()
        if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
            value = value[1:-1]
        out[key] = value
    return out


def _truthy(value: str | None) -> bool:
    return str(value).strip().lower() in {"1", "true", "yes", "on"}


def _csv(value: str | None) -> list[str]:
    if not value:
        return []
    return [p.strip() for p in value.split(",") if p.strip()]


@dataclass
class Settings:
    demo_mode: bool = True
    feed_source: str = "demo"
    feed_file: str = ""
    odds_api_key: str = ""
    odds_api_base: str = "https://api.the-odds-api.com/v4"
    host: str = "127.0.0.1"
    port: int = 8787
    your_books: list[str] = field(default_factory=lambda: ["fanduel", "draftkings"])
    sharp_book: str = "pinnacle"
    devig_method: str = "power"
    default_stake: float = 100.0
    bankroll: float = 2000.0
    kelly_multiplier: float = 0.25
    min_ev: float = 0.0

    @property
    def effective_source(self) -> str:
        """DEMO_MODE=1 pins the feed to fixtures whatever FEED_SOURCE says."""
        return "demo" if self.demo_mode else self.feed_source


def load_settings(env: dict[str, str] | None = None) -> Settings:
    merged = dict(load_dotenv())
    merged.update(os.environ)
    if env:
        merged.update(env)

    def get(key: str, default: str = "") -> str:
        return merged.get(key, default)

    s = Settings()
    s.demo_mode = _truthy(get("DEMO_MODE", "1"))
    s.feed_source = get("FEED_SOURCE", "demo").lower()
    s.feed_file = get("FEED_FILE", "")
    s.odds_api_key = get("ODDS_API_KEY", "")
    s.odds_api_base = get("ODDS_API_BASE", s.odds_api_base)
    s.host = get("HOST", s.host)
    s.port = int(get("PORT", str(s.port)))
    s.your_books = _csv(get("YOUR_BOOKS")) or s.your_books
    s.sharp_book = get("SHARP_BOOK", s.sharp_book)
    s.devig_method = get("DEVIG_METHOD", s.devig_method)
    s.default_stake = float(get("DEFAULT_STAKE", str(s.default_stake)))
    s.bankroll = float(get("BANKROLL", str(s.bankroll)))
    s.kelly_multiplier = float(get("KELLY_MULTIPLIER", str(s.kelly_multiplier)))
    s.min_ev = float(get("MIN_EV", str(s.min_ev)))

    if s.devig_method not in ("multiplicative", "additive", "power"):
        raise ValueError(f"DEVIG_METHOD must be a known method, got {s.devig_method!r}")
    if s.sharp_book and s.sharp_book in s.your_books:
        raise ValueError(
            f"SHARP_BOOK ({s.sharp_book}) is also in YOUR_BOOKS. The sharp line is "
            "the fairness benchmark; a book cannot grade its own price."
        )
    return s
