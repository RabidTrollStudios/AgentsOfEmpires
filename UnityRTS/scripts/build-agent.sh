#!/usr/bin/env bash
# Build a single opponent agent by name.
# Usage: ./build-agent.sh <AgentName> [AgentFile]
# Example: ./build-agent.sh Idle Idle.cs
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$SCRIPT_DIR/.."

AGENT_NAME="${1:?Usage: $0 <AgentName> [AgentFile]}"
AGENT_FILE="${2:-${AGENT_NAME}.cs}"

echo "Building agent: $AGENT_NAME (file: $AGENT_FILE)..."
dotnet build "$PROJECT_ROOT/Opponents/Opponents.csproj" \
    -c Debug \
    -p:AgentName="$AGENT_NAME" \
    -p:AgentFile="$AGENT_FILE"

echo "Agent $AGENT_NAME built -> EnemyAgents/PlanningAgent_${AGENT_NAME}.dll"
