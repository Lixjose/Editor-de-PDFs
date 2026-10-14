#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_CLI_HOME="${TMPDIR:-/tmp}/mythlix-cli"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
if [[ -x .sdk/dotnet ]]; then SDK=.sdk/dotnet; else SDK=dotnet; fi
exec "$SDK" run --project src/Mythlix.Desktop/Mythlix.Desktop.csproj --no-build
