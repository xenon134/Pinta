#!/usr/bin/env bash
# Development launcher for this Pinta fork.
# Builds if needed, then runs the app from a normal terminal.
set -euo pipefail

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

cd "$(dirname "$0")"

if [ "${1:-}" = "--build" ]; then
	dotnet build
fi

exec dotnet build/bin/Pinta.dll "$@"
