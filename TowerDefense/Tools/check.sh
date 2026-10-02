#!/usr/bin/env bash
# One command to validate the Unity project without Unity:
#   1. compile all runtime scripts against UnityEngine stubs
#   2. compile all Editor scripts against UnityEditor stubs
#   3. run the engine-free test suites (NUnit): core, content, platform, visuals
set -euo pipefail
cd "$(dirname "$0")"
dotnet build CompileCheck/Runtime/Runtime.csproj -nologo -clp:ErrorsOnly -v:q
dotnet build CompileCheck/Editor/Editor.csproj -nologo -clp:ErrorsOnly -v:q
dotnet test CoreTests/CoreTests.csproj -nologo -v:q
# Engine-free test suites owned by individual workstreams.
for suite in ContentTests PlatformTests VisualsTests; do
  if [ -f "$suite/$suite.csproj" ]; then
    dotnet test "$suite/$suite.csproj" -nologo -v:q
  fi
done
echo "check.sh: all green"
