"""
The desk's HTTP layer. Stdlib http.server - no framework, no dependencies.

Routes
    GET  /                 the desk UI
    GET  /static/<file>    assets
    GET  /api/health       settings summary and feed source
    GET  /api/markets      normalised feed
    GET  /api/edges        ranked edges  ?stake= &method= &min_ev=
    POST /api/parlay       {"stake": 50, "legs": [{market_id, outcome_index, book}]}

Bound to 127.0.0.1 by default. There is no auth: do not put this on a
public interface as-is.
"""
from __future__ import annotations

import json
import traceback
import urllib.parse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

from . import odds
from .analyze import analyze_all, build_parlay
from .config import ROOT, Settings, load_settings
from .feed import FeedError, load_markets

STATIC = ROOT / "static"
CONTENT_TYPES = {
    ".html": "text/html; charset=utf-8",
    ".css": "text/css; charset=utf-8",
    ".js": "text/javascript; charset=utf-8",
    ".json": "application/json",
    ".svg": "image/svg+xml",
}
MAX_BODY = 256 * 1024

# Inline so the desk has no asset to miss. Served for /favicon.ico, which
# every browser requests unprompted.
FAVICON = (
    b'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">'
    b'<rect width="32" height="32" rx="6" fill="#0c0e12"/>'
    b'<path d="M5 22 L13 12 L19 17 L27 7" fill="none" stroke="#3ddc91" '
    b'stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>'
    b"</svg>"
)


class Desk:
    """Holds settings and the feed so requests do not re-read them."""

    def __init__(self, settings: Settings):
        self.settings = settings
        self.markets, self.source = load_markets(settings)


class Handler(BaseHTTPRequestHandler):
    desk: Desk = None  # set by serve()
    server_version = "betting-desk"
    sys_version = ""

    # ---- plumbing ----

    def log_message(self, fmt: str, *args) -> None:
        print(f"  {self.address_string()} {fmt % args}", flush=True)

    def _send(self, code: int, body: bytes, ctype: str) -> None:
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        if self.command != "HEAD":
            self.wfile.write(body)

    def _json(self, payload, code: int = 200) -> None:
        body = json.dumps(payload, indent=2, allow_nan=False).encode("utf-8")
        self._send(code, body, "application/json")

    def _error(self, code: int, message: str) -> None:
        self._json({"error": message}, code)

    def _static(self, name: str) -> None:
        # Resolve and confirm containment; never trust the request path.
        target = (STATIC / name).resolve()
        if not str(target).startswith(str(STATIC.resolve()) + "/") or not target.is_file():
            self._error(404, "not found")
            return
        ctype = CONTENT_TYPES.get(target.suffix, "application/octet-stream")
        self._send(200, target.read_bytes(), ctype)

    def _query(self) -> dict[str, str]:
        q = urllib.parse.urlparse(self.path).query
        return {k: v[0] for k, v in urllib.parse.parse_qs(q).items()}

    def _float(self, params: dict[str, str], key: str) -> float | None:
        if key not in params:
            return None
        try:
            return float(params[key])
        except ValueError:
            raise ValueError(f"{key} must be a number, got {params[key]!r}")

    def _method(self, params: dict[str, str]) -> str | None:
        m = params.get("method")
        if m is None:
            return None
        if m not in odds.DEVIG_METHODS:
            raise ValueError(
                f"unknown method {m!r}; choose from "
                f"{', '.join(sorted(odds.DEVIG_METHODS))}"
            )
        return m

    # ---- routes ----

    def do_HEAD(self) -> None:
        self.do_GET()

    def do_GET(self) -> None:
        path = urllib.parse.urlparse(self.path).path
        s = self.desk.settings
        try:
            if path in ("/", "/index.html"):
                self._static("index.html")
            elif path == "/favicon.ico":
                self._send(200, FAVICON, "image/svg+xml")
            elif path.startswith("/static/"):
                self._static(path[len("/static/"):])
            elif path == "/api/health":
                self._json({
                    "ok": True,
                    "demo_mode": s.demo_mode,
                    "feed_source": self.desk.source,
                    "markets": len(self.desk.markets),
                    "your_books": s.your_books,
                    "sharp_book": s.sharp_book,
                    "devig_method": s.devig_method,
                    "default_stake": s.default_stake,
                    "bankroll": s.bankroll,
                    "kelly_multiplier": s.kelly_multiplier,
                    "min_ev": s.min_ev,
                })
            elif path == "/api/markets":
                self._json({"source": self.desk.source, "markets": self.desk.markets})
            elif path == "/api/edges":
                p = self._query()
                self._json(analyze_all(
                    self.desk.markets, s,
                    stake=self._float(p, "stake"),
                    method=self._method(p),
                    min_ev=self._float(p, "min_ev"),
                ))
            else:
                self._error(404, f"no route for {path}")
        except ValueError as exc:
            self._error(400, str(exc))
        except FeedError as exc:
            self._error(502, str(exc))
        except Exception:
            traceback.print_exc()
            self._error(500, "internal error")

    def do_POST(self) -> None:
        path = urllib.parse.urlparse(self.path).path
        if path != "/api/parlay":
            self._error(404, f"no route for {path}")
            return
        try:
            length = int(self.headers.get("Content-Length") or 0)
            if length > MAX_BODY:
                self._error(413, "body too large")
                return
            payload = json.loads(self.rfile.read(length) or b"{}")
            legs = payload.get("legs") or []
            if not isinstance(legs, list):
                raise ValueError("legs must be a list")
            self._json(build_parlay(
                legs, self.desk.markets, self.desk.settings,
                stake=payload.get("stake"),
                method=payload.get("method"),
            ))
        except json.JSONDecodeError as exc:
            self._error(400, f"invalid JSON: {exc}")
        except ValueError as exc:
            self._error(400, str(exc))
        except Exception:
            traceback.print_exc()
            self._error(500, "internal error")


def serve(settings: Settings | None = None) -> None:
    settings = settings or load_settings()
    Handler.desk = Desk(settings)
    httpd = ThreadingHTTPServer((settings.host, settings.port), Handler)
    mode = "DEMO" if settings.demo_mode else "LIVE"
    print(f"betting desk [{mode}]  feed={Handler.desk.source}  "
          f"markets={len(Handler.desk.markets)}")
    print(f"  your books : {', '.join(settings.your_books)}")
    print(f"  sharp book : {settings.sharp_book or '(none, consensus only)'}")
    print(f"  de-vig     : {settings.devig_method}")
    print(f"  listening  : http://{settings.host}:{settings.port}")
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\nshutting down")
    finally:
        httpd.server_close()


if __name__ == "__main__":
    serve()
