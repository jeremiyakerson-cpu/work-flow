#!/usr/bin/env bash
# Start the betting desk. Runs the unit tests first - the math is the
# product, so a red suite should never reach a price on screen.
#
#   ./run.sh          test, then serve
#   ./run.sh test     test only
#   SKIP_TESTS=1 ./run.sh
set -euo pipefail

cd "$(dirname "$0")"

PY="${PYTHON:-python3}"
command -v "$PY" >/dev/null 2>&1 || { echo "error: $PY not found" >&2; exit 1; }

if [ ! -f .env ]; then
  echo "note: no .env found, using built-in defaults (demo mode)."
  echo "      cp .env.example .env to configure."
fi

if [ "${1:-}" = "test" ] || [ "${SKIP_TESTS:-0}" != "1" ]; then
  echo "running unit tests..."
  "$PY" -m unittest discover -s tests -t . -q
fi
[ "${1:-}" = "test" ] && exit 0

exec "$PY" -m desk.server
