"""Signs in to Pinterest once (official OAuth) and keeps the token fresh."""
import json
import secrets
import threading
import time
import webbrowser
from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.parse import parse_qs, urlencode, urlparse

import requests

from .config import STATE_DIR

TOKEN_PATH = STATE_DIR / "token.json"
SCOPES = "boards:read,boards:write,pins:read,pins:write,user_accounts:read"


class AuthError(RuntimeError):
    pass


def _save(token):
    token["expires_at"] = time.time() + int(token.get("expires_in", 0))
    if "refresh_token_expires_in" in token:
        token["refresh_expires_at"] = time.time() + int(token["refresh_token_expires_in"])
    TOKEN_PATH.write_text(json.dumps(token, indent=2), encoding="utf-8")
    return token


def _token_request(cfg, data):
    pin = cfg["pinterest"]
    resp = requests.post(
        f"{pin['api_base']}/oauth/token",
        auth=(pin["client_id"], pin["client_secret"]),
        data=data,
        timeout=30,
    )
    if resp.status_code != 200:
        raise AuthError(f"Pinterest refused the sign-in ({resp.status_code}): {resp.text}")
    return resp.json()


def login(cfg):
    """Opens Pinterest's own sign-in page and catches the reply on localhost."""
    pin = cfg["pinterest"]
    if not pin["client_id"] or not pin["client_secret"]:
        raise AuthError("Add your app's client_id and client_secret to config.yaml first (see README).")

    redirect = urlparse(pin["redirect_uri"])
    state = secrets.token_urlsafe(16)
    result = {}

    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            query = parse_qs(urlparse(self.path).query)
            if query.get("state", [None])[0] != state:
                result["error"] = "State did not match; please try again."
            elif "code" in query:
                result["code"] = query["code"][0]
            else:
                result["error"] = query.get("error", ["Sign-in was cancelled."])[0]
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.end_headers()
            msg = "Signed in! You can close this tab." if "code" in result else result.get("error")
            self.wfile.write(f"<h2 style='font-family:sans-serif'>{msg}</h2>".encode())

        def log_message(self, *args):
            pass

    server = HTTPServer((redirect.hostname, redirect.port or 80), Handler)
    thread = threading.Thread(target=server.handle_request, daemon=True)
    thread.start()

    url = "https://www.pinterest.com/oauth/?" + urlencode({
        "client_id": pin["client_id"],
        "redirect_uri": pin["redirect_uri"],
        "response_type": "code",
        "scope": SCOPES,
        "state": state,
    })
    print("Opening Pinterest sign-in in your browser...")
    webbrowser.open(url)
    thread.join(timeout=300)
    server.server_close()

    if "code" not in result:
        raise AuthError(result.get("error", "Timed out waiting for sign-in."))
    token = _token_request(cfg, {
        "grant_type": "authorization_code",
        "code": result["code"],
        "redirect_uri": pin["redirect_uri"],
    })
    _save(token)
    print("Signed in to Pinterest.")
    return token


def get_access_token(cfg, force_refresh=False):
    if not TOKEN_PATH.exists():
        return login(cfg)["access_token"]
    token = json.loads(TOKEN_PATH.read_text(encoding="utf-8"))
    if force_refresh or time.time() > token.get("expires_at", 0) - 300:
        try:
            fresh = _token_request(cfg, {
                "grant_type": "refresh_token",
                "refresh_token": token["refresh_token"],
            })
            fresh.setdefault("refresh_token", token["refresh_token"])
            token = _save(fresh)
        except (AuthError, KeyError):
            token = login(cfg)
    return token["access_token"]
