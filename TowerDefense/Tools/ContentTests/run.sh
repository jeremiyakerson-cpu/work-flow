#!/usr/bin/env bash
# Engine-free content tests (levels, layout validator, progression).
# Run alongside Tools/check.sh.
set -euo pipefail
cd "$(dirname "$0")"
dotnet test ContentTests.csproj -nologo -v:q
echo "content tests: all green"
