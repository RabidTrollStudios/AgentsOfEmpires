#!/usr/bin/env bash
# Build all projects and deploy DLLs to Unity.
# Usage: ./build-all.sh [--skip-tests]
#
# Step 1: Build the solution (AgentSDK + AgentTestHarness + PlanningAgent +
#         Gameplay.Tests + Opponent.Tests + Parity.Tests + ParityRunner).
#         MSBuild handles dependency order — if AgentSDK changes, all
#         dependents rebuild automatically.
#
# Step 2: Build all opponent agents via BuildAllOpponents.proj.
#         Each *.cs in Opponents/ becomes a separate DLL, auto-copied
#         to EnemyAgents/ by the CopyToEnemyAgents target.
#
# Step 3: Deploy shared DLLs to Unity's Plugins folder.
#
# Step 4 (optional): Run smoke and parity tests.

set -euo pipefail
cd "$(dirname "$0")"

SKIP_TESTS=false
if [[ "${1:-}" == "--skip-tests" ]]; then
    SKIP_TESTS=true
fi

echo "=== Step 1: Building solution (AgentSDK + dependents) ==="
dotnet build UnityRTS.slnx
echo ""

echo "=== Step 2: Building all opponent agents ==="
dotnet msbuild Opponents/BuildAllOpponents.proj
echo ""

echo "=== Step 3: Deploying DLLs to Unity ==="
cp AgentSDK/bin/Debug/netstandard2.1/AgentSDK.dll         RTS/Assets/Plugins/AgentSDK.dll
cp AgentTestHarness/bin/Debug/netstandard2.1/AgentTestHarness.dll RTS/Assets/Plugins/AgentTestHarness.dll
echo "  AgentSDK.dll         -> RTS/Assets/Plugins/"
echo "  AgentTestHarness.dll -> RTS/Assets/Plugins/"
echo "  Opponent DLLs        -> EnemyAgents/ (via MSBuild CopyToEnemyAgents)"
echo ""

if [ "$SKIP_TESTS" = false ]; then
    echo "=== Step 4: Running tests ==="
    echo "--- DLL smoke tests ---"
    dotnet test Opponent.Tests/Opponent.Tests.csproj --filter "FullyQualifiedName~DllSmokeTests" --no-build -v q

    echo "--- Parity tests ---"
    dotnet test Parity.Tests/Parity.Tests.csproj --filter "FullyQualifiedName~Parity" --no-build -v q
    echo ""
fi

echo "All builds succeeded."
