#!/usr/bin/env bash
# Build the AgentSDK (dependency for all agents)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$SCRIPT_DIR/.."

echo "Building AgentSDK..."
dotnet build "$PROJECT_ROOT/AgentSDK/AgentSDK.csproj" -c Debug
echo "AgentSDK built successfully."
