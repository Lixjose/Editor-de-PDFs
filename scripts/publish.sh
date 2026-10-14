#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
rid="${1:-linux-x64}"
case "$rid" in linux-x64|win-x64) ;; *) echo 'Use linux-x64 o win-x64'; exit 1;; esac
export DOTNET_CLI_HOME="${TMPDIR:-/tmp}/mythlix-cli"
if [[ -x .sdk/dotnet ]]; then SDK=.sdk/dotnet; else SDK=dotnet; fi
"$SDK" publish src/Mythlix.Desktop/Mythlix.Desktop.csproj -c Release -r "$rid" --self-contained true -m:1 -o "artifacts/$rid"
