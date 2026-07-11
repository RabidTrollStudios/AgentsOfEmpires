#!/usr/bin/env bash
# Run the full test suite (Gameplay.Tests, Opponent.Tests, Parity.Tests via the solution).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$SCRIPT_DIR/.."

echo "Running tests..."
dotnet test "$PROJECT_ROOT/UnityRTS.slnx" \
    --nologo \
    "$@"
