"""Pinterest organizer.

    python main.py open           Open Pinterest in the browser (sign in there once)
    python main.py login          Connect this tool to your Pinterest account
    python main.py sort           Preview how your pins would be sorted (changes nothing)
    python main.py sort --apply   Sort your pins into categories for real
    python main.py discover       Open the pin picker page
"""
import argparse
import sys

from pinboard.config import CONFIG_PATH, load_config


def main():
    parser = argparse.ArgumentParser(description="Open, sort and discover Pinterest pins.")
    sub = parser.add_subparsers(dest="cmd", required=True)
    sub.add_parser("open", help="open Pinterest in the tool's browser")
    sub.add_parser("login", help="connect to your Pinterest account")
    sort = sub.add_parser("sort", help="sort your existing pins")
    sort.add_argument("--apply", action="store_true", help="actually move pins (default: preview)")
    sub.add_parser("discover", help="open the pin picker page")
    args = parser.parse_args()

    if not CONFIG_PATH.exists() and args.cmd != "open":
        sys.exit("config.yaml is missing. Copy config.example.yaml to config.yaml and fill it in.")
    cfg = load_config()

    if args.cmd == "open":
        from pinboard.discover import open_pinterest
        open_pinterest()
    elif args.cmd == "login":
        from pinboard.auth import login
        login(cfg)
    elif args.cmd == "sort":
        from pinboard.api import Pinterest
        from pinboard.categorize import Categorizer
        from pinboard.sorter import sort_pins
        sort_pins(Pinterest(cfg), Categorizer(cfg), cfg, apply=args.apply)
    elif args.cmd == "discover":
        from pinboard.api import Pinterest
        from pinboard.webapp import run
        run(cfg, lambda: Pinterest(cfg))


if __name__ == "__main__":
    main()
