#!/usr/bin/env bash
# Build all projects that depend on AgentSDK.
# Usage: ./build-all.sh
#
# The solution (UnityRTS.slnx) handles AgentSDK, AgentTestHarness, PlanningAgent,
# PlanningAgent.Tests, and ParityRunner via ProjectReference — MSBuild rebuilds
# dependents automatically when AgentSDK changes.
#
# Opponents is built separately because each agent is a distinct invocation
# with -p:AgentName / -p:AgentFile overrides.

set -euo pipefail
cd "$(dirname "$0")"

echo "=== Building solution (AgentSDK + dependents) ==="
dotnet build UnityRTS.slnx

echo ""
echo "=== Building opponent agents ==="
failed=0
for f in Opponents/*.cs; do
    name=$(basename "$f" .cs)
    echo -n "  $name ... "
    if dotnet build Opponents/Opponents.csproj -p:AgentName="$name" -p:AgentFile="$(basename "$f")" -v q 2>&1 | tail -1; then
        :
    else
        echo "FAILED"
        failed=1
    fi
done

if [ $failed -ne 0 ]; then
    echo ""
    echo "Some opponent builds failed!"
    exit 1
fi

echo ""
echo "=== Running DLL smoke tests ==="
dotnet test PlanningAgent.Tests/PlanningAgent.Tests.csproj --filter "FullyQualifiedName~DllSmokeTests" --no-build -v q

echo ""
echo "All builds and tests succeeded."
