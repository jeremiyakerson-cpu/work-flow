"""Finds pins you might like by reading Pinterest search pages in a real browser.

Pinterest's public API has no general search, so this opens Pinterest in a
browser window that stays signed in (its own profile, under state/), reads the
search results the way you would see them, and hands back each pin's id,
picture and description. It only reads. It never likes, saves or follows
anything: saving happens later, through the official API, for the pins you tick.
It goes slowly on purpose and only runs when you press Refresh.
"""
import json
import random
import time
from urllib.parse import quote_plus

from .config import STATE_DIR

PROFILE_DIR = STATE_DIR / "browser-profile"
SEEN_PATH = STATE_DIR / "seen.json"

COLLECT_JS = """
() => {
  const out = [];
  for (const a of document.querySelectorAll('a[href*="/pin/"]')) {
    const m = a.getAttribute('href').match(/\\/pin\\/(\\d+)/);
    const img = a.querySelector('img');
    if (!m || !img) continue;
    let src = img.currentSrc || img.src || '';
    if (!src.includes('pinimg.com')) continue;
    src = src.replace(/\\/(?:60x60|75x75_RS|170x|236x)\\//, '/474x/');  // a sharper size
    out.push({id: m[1], image: src, title: img.alt || a.getAttribute('aria-label') || ''});
  }
  return out;
}
"""


SIGNED_OUT_JS = """
() => !!document.querySelector('[data-test-id="gated-search-pins-feed"], [data-test-id="login-button"]')
"""


class NotSignedIn(RuntimeError):
    pass


def load_seen():
    if SEEN_PATH.exists():
        return set(json.loads(SEEN_PATH.read_text(encoding="utf-8")))
    return set()


def mark_seen(pin_ids):
    seen = load_seen() | set(pin_ids)
    SEEN_PATH.write_text(json.dumps(sorted(seen)), encoding="utf-8")


def _browser(playwright, show):
    PROFILE_DIR.mkdir(parents=True, exist_ok=True)
    return playwright.chromium.launch_persistent_context(
        str(PROFILE_DIR), headless=not show, viewport={"width": 1280, "height": 900},
    )


def open_pinterest(show=True):
    """Opens Pinterest in the tool's own browser so you can sign in once."""
    from playwright.sync_api import sync_playwright

    with sync_playwright() as p:
        ctx = _browser(p, show=True)
        page = ctx.pages[0] if ctx.pages else ctx.new_page()
        page.goto("https://www.pinterest.com/")
        print("Pinterest is open. Sign in if asked, then close the browser window when done.")
        try:
            page.wait_for_event("close", timeout=0)
        except Exception:
            pass
        ctx.close()


def find_pins(queries, per_query, show=True, exclude=()):
    """Returns a list of {id, image, title, query} for each search phrase."""
    from playwright.sync_api import sync_playwright

    skip = load_seen() | set(exclude)
    results, found_ids = [], set()
    with sync_playwright() as p:
        ctx = _browser(p, show)
        page = ctx.pages[0] if ctx.pages else ctx.new_page()
        for query in queries:
            page.goto(f"https://www.pinterest.com/search/pins/?q={quote_plus(query)}",
                      wait_until="domcontentloaded")
            page.wait_for_timeout(3000)
            if page.evaluate(SIGNED_OUT_JS):
                ctx.close()
                raise NotSignedIn("The pin finder's browser isn't signed in to Pinterest. "
                                  "In the menu, choose option 1 (Open Pinterest), sign in, then try again.")
            got = 0
            for _ in range(12):  # scroll a few screens, like a person would
                time.sleep(random.uniform(1.5, 3.0))
                for pin in page.evaluate(COLLECT_JS):
                    if pin["id"] in skip or pin["id"] in found_ids:
                        continue
                    found_ids.add(pin["id"])
                    pin["query"] = query
                    results.append(pin)
                    got += 1
                    if got >= per_query:
                        break
                if got >= per_query:
                    break
                page.mouse.wheel(0, random.randint(1400, 2200))
            print(f"  '{query}': {got} new pins")
            time.sleep(random.uniform(2, 4))
        ctx.close()
    if not results and not skip:
        raise RuntimeError("Pinterest loaded but no pins could be read. Pinterest may have changed its "
                           "page layout; the reader in pinboard/discover.py needs updating.")
    return results
