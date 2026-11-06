#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="http://0.0.0.0:3000"
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=anthology;Username=anthology;Password=anthology"

for _ in $(seq 1 180); do
  if (echo > /dev/tcp/127.0.0.1/5433) >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

cd src/Anthology/ClientApp
npm run generate
npx vite build
cd ../../..

exec dotnet run --project src/Anthology --no-launch-profile
