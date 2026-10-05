"""Shared test setup. Keeps every test off the real history, alerts and scheduler files."""
import os
import sys

import pytest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


@pytest.fixture(autouse=True)
def _isolated_history(tmp_path, monkeypatch):
    from app import main
    monkeypatch.setattr(main, "HISTORY_DB", str(tmp_path / "history.sqlite3"))
    monkeypatch.setattr(main, "_history", None)
    monkeypatch.setattr(main, "ALERTS_FILE", str(tmp_path / "alerts.json"))
    monkeypatch.setattr(main, "_alerts", None)
    monkeypatch.setattr(main, "SCHEDULER_STATE", str(tmp_path / "scheduler.json"))
    monkeypatch.setattr(main, "_scheduler", None)
