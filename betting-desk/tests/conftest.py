"""Shared test setup. Keeps every test off the real history database."""
import os
import sys

import pytest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


@pytest.fixture(autouse=True)
def _isolated_history(tmp_path, monkeypatch):
    from app import main
    monkeypatch.setattr(main, "HISTORY_DB", str(tmp_path / "history.sqlite3"))
    monkeypatch.setattr(main, "_history", None)
