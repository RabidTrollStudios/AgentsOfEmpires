#!/bin/bash
# Builds all opponent agents into individual DLLs in EnemyAgents/
# Usage: bash build_all.sh

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

agents=(
    "Idle"
    "Gatherer"
    "ArcherOnly"
    "SoldierRush"
    "ArcherSwarm"
    "Turtle"
    "Balanced"
    "EconBoom"
    "Swarm"
    "Commander"
)

FAILED=0
for agent in "${agents[@]}"; do
    echo "Building PlanningAgent_${agent}..."
    dotnet build -p:AgentName="$agent" -p:AgentFile="${agent}.cs" --nologo -v quiet 2>&1
    if [ $? -ne 0 ]; then
        echo "  FAILED: $agent"
        FAILED=$((FAILED + 1))
    else
        echo "  OK"
    fi
done

echo ""
if [ $FAILED -eq 0 ]; then
    echo "All ${#agents[@]} opponents built successfully."
else
    echo "$FAILED of ${#agents[@]} opponents failed to build."
    exit 1
fi
