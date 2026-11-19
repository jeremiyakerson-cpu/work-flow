"""Small wrapper around the official Pinterest API v5."""
import time

import requests

from .auth import get_access_token


class PinterestError(RuntimeError):
    pass


class Pinterest:
    def __init__(self, cfg):
        self.cfg = cfg
        self.base = cfg["pinterest"]["api_base"]
        self.session = requests.Session()
        self._set_token(get_access_token(cfg))
        self._boards = None
        self._sections = {}

    def _set_token(self, token):
        self.session.headers["Authorization"] = f"Bearer {token}"

    def _call(self, method, path, retried=False, **kwargs):
        resp = self.session.request(method, self.base + path, timeout=30, **kwargs)
        if resp.status_code == 401 and not retried:
            self._set_token(get_access_token(self.cfg, force_refresh=True))
            return self._call(method, path, retried=True, **kwargs)
        if resp.status_code == 429 and not retried:
            wait = int(resp.headers.get("Retry-After", "30"))
            print(f"  Pinterest asked us to slow down; waiting {wait}s...")
            time.sleep(wait)
            return self._call(method, path, retried=True, **kwargs)
        if resp.status_code >= 400:
            raise PinterestError(f"{method} {path} failed ({resp.status_code}): {resp.text[:300]}")
        return resp.json() if resp.content else {}

    def _paged(self, path, **params):
        params.setdefault("page_size", 100)
        while True:
            data = self._call("GET", path, params=params)
            yield from data.get("items", [])
            if not data.get("bookmark"):
                return
            params["bookmark"] = data["bookmark"]

    # ---- boards & sections -------------------------------------------------
    def boards(self):
        if self._boards is None:
            self._boards = list(self._paged("/boards"))
        return self._boards

    def board_pins(self, board_id):
        return self._paged(f"/boards/{board_id}/pins")

    def find_or_create_board(self, name, privacy="PUBLIC"):
        for board in self.boards():
            if board["name"].strip().lower() == name.strip().lower():
                return board
        board = self._call("POST", "/boards", json={"name": name, "privacy": privacy})
        self._boards.append(board)
        print(f"  + created board '{name}'")
        return board

    def sections(self, board_id):
        if board_id not in self._sections:
            self._sections[board_id] = list(self._paged(f"/boards/{board_id}/sections"))
        return self._sections[board_id]

    def find_or_create_section(self, board_id, name):
        for section in self.sections(board_id):
            if section["name"].strip().lower() == name.strip().lower():
                return section
        section = self._call("POST", f"/boards/{board_id}/sections", json={"name": name})
        self._sections[board_id].append(section)
        print(f"  + created section '{name}'")
        return section

    # ---- pins ---------------------------------------------------------------
    def move_pin(self, pin_id, board_id, section_id=None):
        body = {"board_id": board_id}
        if section_id:
            body["board_section_id"] = section_id
        return self._call("PATCH", f"/pins/{pin_id}", json=body)

    def save_pin(self, pin_id, board_id, section_id=None, image_url=None, title=""):
        """Saves someone else's pin to your board. Falls back to a new pin from the image."""
        body = {"board_id": board_id}
        if section_id:
            body["board_section_id"] = section_id
        try:
            return self._call("POST", f"/pins/{pin_id}/save", json=body)
        except PinterestError:
            if not image_url:
                raise
        body.update({
            "title": title[:100],
            "link": f"https://www.pinterest.com/pin/{pin_id}/",
            "media_source": {"source_type": "image_url", "url": image_url},
        })
        return self._call("POST", "/pins", json=body)


def pin_text(pin):
    """All the words on a pin that say what it is about."""
    parts = [pin.get("title"), pin.get("description"), pin.get("alt_text"), pin.get("note")]
    link = pin.get("link") or ""
    if link:
        parts.append(link.split("//")[-1].split("/")[0].replace("www.", "").replace(".", " "))
    return " ".join(p for p in parts if p)


def pin_image(pin):
    images = (pin.get("media") or {}).get("images") or {}
    for size in ("600x", "400x300", "1200x", "150x150"):
        if size in images:
            return images[size]["url"]
    return None
