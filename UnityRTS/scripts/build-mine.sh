#!/usr/bin/env bash
# Build the player's agent (Mine) from PlanningAgent project
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$SCRIPT_DIR/.."

echo "Building PlanningAgent (Mine)..."
dotnet build "$PROJECT_ROOT/PlanningAgent/PlanningAgent.csproj" -c Debug
echo "Mine built -> EnemyAgents/PlanningAgent_Mine.dll"
