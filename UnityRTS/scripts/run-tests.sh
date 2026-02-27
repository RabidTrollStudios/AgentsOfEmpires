#!/usr/bin/env bash
# Run the PlanningAgent test suite
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$SCRIPT_DIR/.."

echo "Running tests..."
dotnet test "$PROJECT_ROOT/PlanningAgent.Tests/PlanningAgent.Tests.csproj" \
    --nologo \
    "$@"
