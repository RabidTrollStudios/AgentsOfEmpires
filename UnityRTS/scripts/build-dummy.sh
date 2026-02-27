#!/usr/bin/env bash
# Build the Dummy test agent
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$SCRIPT_DIR/.."

echo "Building Dummy agent..."
dotnet build "$PROJECT_ROOT/Opponents/Opponents.csproj" \
    -c Debug \
    -p:AgentName=Dummy \
    -p:AgentFile="../PlanningAgent/PlanningAgent_Dummy.cs"
echo "Dummy built -> EnemyAgents/PlanningAgent_Dummy.dll"
