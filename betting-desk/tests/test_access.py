"""
DESK_PASSWORD: a deployed instance answers nothing but /healthz without it.

Run: python3 -m pytest tests/test_access.py
"""
import pytest
from fastapi.testclient import TestClient

from app import main


@pytest.fixture
def client(monkeypatch):
    monkeypatch.setattr(main, "DEMO_MODE", True)
    return TestClient(main.app)


def test_open_when_no_password(client):
    assert client.get("/api/health").status_code == 200
    assert client.get("/").status_code == 200


def test_password_required_everywhere_but_healthz(client, monkeypatch):
    monkeypatch.setattr(main, "DESK_PASSWORD", "s3cret")
    for path in ("/", "/api/health", "/api/board/nfl", "/api/alerts", "/docs", "/openapi.json"):
        r = client.get(path)
        assert r.status_code == 401, path
        assert r.headers["www-authenticate"].startswith("Basic")
    assert client.get("/healthz").json() == {"ok": True}


def test_any_username_right_password(client, monkeypatch):
    monkeypatch.setattr(main, "DESK_PASSWORD", "s3cret")
    assert client.get("/api/health", auth=("me", "s3cret")).status_code == 200
    assert client.get("/api/health", auth=("", "s3cret")).json()["password_protected"] is True
    assert client.get("/api/health", auth=("me", "wrong")).status_code == 401
    assert client.get("/api/health", headers={"Authorization": "Basic !!!"}).status_code == 401
    assert client.get("/api/health", headers={"Authorization": "Bearer s3cret"}).status_code == 401
