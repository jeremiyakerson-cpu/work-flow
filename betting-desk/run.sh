#!/usr/bin/env bash
set -e
[ -d .venv ] || python3 -m venv .venv
source .venv/bin/activate
pip install -q -r requirements.txt
[ -f .env ] && set -a && source .env && set +a
exec uvicorn app.main:app --reload --port 8000
