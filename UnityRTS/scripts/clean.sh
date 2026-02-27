#!/usr/bin/env bash
# Clean all build artifacts
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$SCRIPT_DIR/.."

echo "Cleaning build artifacts..."

for proj in AgentSDK PlanningAgent Opponents AgentTestHarness PlanningAgent.Tests; do
    if [ -d "$PROJECT_ROOT/$proj" ]; then
        echo "  Cleaning $proj..."
        dotnet clean "$PROJECT_ROOT/$proj" --nologo -v quiet 2>/dev/null || true
    fi
done

echo "Removing EnemyAgents DLLs..."
rm -f "$PROJECT_ROOT/EnemyAgents/"*.dll
rm -f "$PROJECT_ROOT/EnemyAgents/"*.pdb
rm -f "$PROJECT_ROOT/EnemyAgents/"*.xml

echo "Clean complete."
