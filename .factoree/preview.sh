#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

export MISE_CONFIG_FILE="$root/.factoree/mise.toml"
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://0.0.0.0:5018

(
  cd src/Anthology/ClientApp
  npm run generate
  npm run build
)

exec dotnet run --project src/Anthology --no-launch-profile
