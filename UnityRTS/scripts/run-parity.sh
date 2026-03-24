#!/usr/bin/env bash
# Run the sim/engine parity tests to verify SimGame stays in sync with the game engine.
# These catch divergences between the AgentTestHarness simulation and the Unity game.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$SCRIPT_DIR/.."

echo "Running parity tests..."
dotnet test "$PROJECT_ROOT/PlanningAgent.Tests/PlanningAgent.Tests.csproj" \
    --nologo \
    --filter "FullyQualifiedName~Parity" \
    "$@"
