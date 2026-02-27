#!/usr/bin/env bash
# Build all opponent agents and copy DLLs to EnemyAgents/
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$SCRIPT_DIR/.."

AGENTS=(
    "Idle:Idle.cs"
    "ArcherOnly:ArcherOnly.cs"
    "Balanced:Balanced.cs"
    "Commander:Commander.cs"
    "EconBoom:EconBoom.cs"
    "Gatherer:Gatherer.cs"
    "SoldierRush:SoldierRush.cs"
    "Swarm:Swarm.cs"
    "Phalanx:Phalanx.cs"
    "Volley:Volley.cs"
    "ArcherSwarm:ArcherSwarm.cs"
    "Turtle:Turtle.cs"
)

FAILED=0

for entry in "${AGENTS[@]}"; do
    IFS=':' read -r name file <<< "$entry"
    echo "=== Building $name ==="
    if dotnet build "$PROJECT_ROOT/Opponents/Opponents.csproj" \
        -c Debug \
        -p:AgentName="$name" \
        -p:AgentFile="$file" \
        --nologo -v quiet; then
        echo "  OK"
    else
        echo "  FAILED"
        FAILED=$((FAILED + 1))
    fi
done

echo ""
echo "Build complete: $((${#AGENTS[@]} - FAILED))/${#AGENTS[@]} succeeded."
[ "$FAILED" -eq 0 ] || exit 1
