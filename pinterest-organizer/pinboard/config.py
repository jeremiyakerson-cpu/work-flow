"""Loads config.yaml and holds the paths for saved state."""
import os
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parent.parent
STATE_DIR = Path(os.environ.get("PINBOARD_STATE_DIR", ROOT / "state"))
CONFIG_PATH = ROOT / "config.yaml"
STATE_DIR.mkdir(parents=True, exist_ok=True)

DEFAULTS = {
    "pinterest": {
        "client_id": "",
        "client_secret": "",
        "redirect_uri": "http://localhost:8085/callback",
        "api_base": "https://api.pinterest.com/v5",
    },
    "sorting": {
        # "sections": every category becomes a section inside one board.
        # "boards":   every category becomes its own board.
        "organize_as": "sections",
        "sections_board": "Sorted Pins",
        "board_privacy": "PUBLIC",
        "unsorted_name": "Unsorted",
        "min_pins_for_new_category": 4,
        "max_new_categories": 10,
        "skip_boards": [],
        "only_boards": [],
    },
    "discover": {
        "queries_per_refresh": 3,
        "pins_per_query": 25,
        "extra_queries": [],
        "show_browser": True,
        "port": 5000,
    },
    "categories": {},
}


def _merge(base, override):
    out = dict(base)
    for key, value in (override or {}).items():
        if isinstance(value, dict) and isinstance(out.get(key), dict):
            out[key] = _merge(out[key], value)
        else:
            out[key] = value
    return out


def load_config():
    data = {}
    if CONFIG_PATH.exists():
        with open(CONFIG_PATH, encoding="utf-8") as fh:
            data = yaml.safe_load(fh) or {}
    cfg = _merge(DEFAULTS, data)
    # Environment variables win, so secrets can stay out of the file.
    pin = cfg["pinterest"]
    pin["client_id"] = os.environ.get("PINTEREST_CLIENT_ID", pin["client_id"])
    pin["client_secret"] = os.environ.get("PINTEREST_CLIENT_SECRET", pin["client_secret"])
    return cfg
