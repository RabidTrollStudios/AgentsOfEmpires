#!/usr/bin/env bash
# Build everything and run tests
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=== Building all agents ==="
"$SCRIPT_DIR/rebuild-enemy-agents.sh"
echo ""

echo "=== Running tests ==="
"$SCRIPT_DIR/run-tests.sh"
echo ""

echo "=== Build and test complete ==="
