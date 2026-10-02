#!/usr/bin/env bash
# One command to validate the Unity project without Unity:
#   1. compile all runtime scripts against UnityEngine stubs
#   2. compile all Editor scripts against UnityEditor stubs
#   3. run the engine-free core tests (NUnit)
set -euo pipefail
cd "$(dirname "$0")"
dotnet build CompileCheck/Runtime/Runtime.csproj -nologo -clp:ErrorsOnly -v:q
dotnet build CompileCheck/Editor/Editor.csproj -nologo -clp:ErrorsOnly -v:q
dotnet test CoreTests/CoreTests.csproj -nologo -v:q
echo "check.sh: all green"
