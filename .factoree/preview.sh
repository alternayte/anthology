#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export ASPNETCORE_URLS=http://127.0.0.1:5018
export ConnectionStrings__DefaultConnection="${ConnectionStrings__DefaultConnection:-Host=localhost;Port=5433;Database=anthology;Username=anthology;Password=anthology}"

(
  cd src/Anthology/ClientApp
  npm run generate
  npm run build
)

exec dotnet run --project src/Anthology --no-launch-profile
