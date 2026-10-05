"""
Browser smoke test for the UI, against a real server in demo mode.

Loads the board, applies filters, tracks a play, and checks it shows up in
the tracker, gets graded in the Results view, and exports to CSV. Then the
live layer: the auto-refresh budget meter, alert settings, an edge alert
toasting in-page and landing in the history, and a manual scheduler run.
Runs at phone width to catch horizontal overflow too.

Needs Playwright and a Chromium:  pip install -r requirements-dev.txt
then `playwright install chromium`, or point BD_CHROMIUM at an existing
binary. Skips (does not fail) when neither is available.

Run: python3 -m pytest tests/test_ui_smoke.py
"""
import os
import socket
import subprocess
import sys
import time
import urllib.request

import pytest

sync_api = pytest.importorskip("playwright.sync_api")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def _free_port() -> int:
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


@pytest.fixture(scope="module")
def server():
    port = _free_port()
    env = {**os.environ, "DEMO_MODE": "1", "ODDS_API_KEY": "", "DESK_PASSWORD": "",
           "AUTO_REFRESH": "1", "AUTO_REFRESH_LEAGUES": "mlb,nba"}
    proc = subprocess.Popen(
        [sys.executable, "-m", "uvicorn", "app.main:app", "--port", str(port), "--log-level", "warning"],
        cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    url = f"http://127.0.0.1:{port}"
    for _ in range(100):
        try:
            urllib.request.urlopen(url + "/api/health", timeout=1)
            break
        except OSError:
            if proc.poll() is not None:
                pytest.fail("server exited: " + proc.stdout.read().decode(errors="replace"))
            time.sleep(0.1)
    else:
        proc.kill()
        pytest.fail("server did not come up")
    yield url
    proc.terminate()
    proc.wait(timeout=10)


@pytest.fixture(scope="module")
def browser():
    exe = os.getenv("BD_CHROMIUM") or next(
        (p for p in ("/opt/pw-browsers/chromium",) if os.path.exists(p)), None)
    with sync_api.sync_playwright() as p:
        try:
            b = p.chromium.launch(executable_path=exe) if exe else p.chromium.launch()
        except Exception as e:   # no browser installed: skip, don't fail
            pytest.skip(f"Chromium not available: {e}")
        yield b
        b.close()


def test_board_filter_track_results(server, browser, tmp_path):
    page = browser.new_page(viewport={"width": 375, "height": 800}, accept_downloads=True)
    errors = []
    page.on("pageerror", lambda e: errors.append(str(e)))

    page.goto(server + "/")
    page.click("#go")
    page.wait_for_selector(".game .cell")
    assert page.locator(".game").count() >= 3
    assert "DEMO" in page.inner_text("#credits")

    # market filter: only Total sections remain
    page.select_option("#f-market", "totals")
    heads = page.locator(".gamegrid .mkt h3").all_inner_texts()
    assert heads and all(h.upper().startswith("TOTAL") for h in heads)

    # min edge that nothing in the demo clears: best play disappears, board stays
    page.fill("#f-edge", "50")
    page.dispatch_event("#f-edge", "input")
    assert page.locator(".play.best").count() == 0
    page.fill("#f-edge", "0")
    page.dispatch_event("#f-edge", "input")
    page.select_option("#f-market", "all")

    # filters persist across a reload
    page.select_option("#f-book", "fanduel")
    page.reload()
    page.click("#go")
    page.wait_for_selector(".game .cell")
    assert page.input_value("#f-book") == "fanduel"
    page.select_option("#f-book", "best")

    # track the first moneyline price
    cell = page.locator(".gamegrid .cell:has(.trk)").first
    side = cell.locator(".lb .nm").inner_text()
    cell.locator(".trk").click()
    assert "(1)" in page.inner_text("#tabs")

    # it's in the tracker
    page.click("#tabs >> text=Tracked")
    page.wait_for_selector(".tk")
    assert side in page.inner_text("#out")

    # results view grades it (demo mode: synthetic final) and charts it
    page.click("[data-tview=results]")
    page.wait_for_selector(".tk .res.won, .tk .res.lost, .tk .res.push", timeout=10000)
    assert page.locator(".stat").first.inner_text().split()[0].count("-") >= 1
    assert page.locator(".chart svg").count() >= 1
    assert "made up" in page.inner_text("#out")

    # CSV export has a header and our row
    with page.expect_download() as dl:
        page.click("[data-act=csv]")
    path = tmp_path / "t.csv"
    dl.value.save_as(path)
    lines = path.read_text().strip().splitlines()
    assert lines[0].startswith("logged_at,league,game")
    assert len(lines) == 2 and side in lines[1]

    # no sideways scrolling at phone width, no script errors
    assert page.evaluate("document.documentElement.scrollWidth") <= 375
    assert errors == []


def test_alerts_meter_and_scheduler(server, browser):
    page = browser.new_page(viewport={"width": 375, "height": 800})
    errors = []
    page.on("pageerror", lambda e: errors.append(str(e)))
    page.goto(server + "/")

    # budget meter in the header: demo runs cost nothing, so 0 of the default 15
    page.wait_for_function("document.querySelector('#auto').textContent.includes('/15')")
    assert "0/15" in page.inner_text("#auto")

    # alert settings: a threshold the demo MLB board's +0.5% play clears
    page.click("#auto")                                   # the meter opens the Alerts tab
    page.wait_for_selector("#al-edge")
    assert "Auto-refresh" in page.inner_text("#out")
    page.fill("#al-edge", "0.3")
    page.click("[data-act=al-save]")
    page.wait_for_function("document.querySelector('#al-edge') && document.querySelector('#al-edge').value === '0.3'")

    # a refresh finds the play: an in-page toast, then it's in the history
    page.click("#tabs >> text=Board")
    page.click("#go")
    page.wait_for_selector(".toast", timeout=10000)
    assert "%" in page.inner_text(".toast")
    page.click("#tabs >> text=Alerts")
    page.wait_for_selector(".al")
    assert "edge" in page.inner_text(".al").lower()

    # a manual scheduler run shows up in the recent-runs log
    page.click("[data-sch-run=nba]")
    page.wait_for_function("document.querySelector('#out').innerText.includes('Recent:')", timeout=10000)
    assert "NBA" in page.inner_text("#out")

    assert page.evaluate("document.documentElement.scrollWidth") <= 375
    assert errors == []
