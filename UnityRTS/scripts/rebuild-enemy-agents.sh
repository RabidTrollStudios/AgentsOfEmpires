#!/usr/bin/env bash
# Full rebuild: SDK + Mine + Dummy + all opponents -> EnemyAgents/
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=== Step 0: Clean stale CSV and log files ==="
rm -f "$SCRIPT_DIR/../EnemyAgents/"PlanningAgent_*.csv
rm -f "$SCRIPT_DIR/../EnemyAgents/"CommandLog_*.txt
echo ""

echo "=== Step 1: Build AgentSDK ==="
"$SCRIPT_DIR/build-sdk.sh"
echo ""

echo "=== Step 2: Build Mine ==="
"$SCRIPT_DIR/build-mine.sh"
echo ""

echo "=== Step 3: Build Dummy ==="
"$SCRIPT_DIR/build-dummy.sh"
echo ""

echo "=== Step 4: Build All Opponents ==="
"$SCRIPT_DIR/build-all-agents.sh"
echo ""

echo "=== All agents rebuilt ==="
ls -la "$SCRIPT_DIR/../EnemyAgents/"*.dll 2>/dev/null | wc -l
echo "DLLs in EnemyAgents/"
