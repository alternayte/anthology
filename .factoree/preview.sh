#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

export MISE_CONFIG_FILE="$ROOT/.factoree/mise.toml"
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://localhost:3000
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=anthology;Username=anthology;Password=anthology"

for _ in $(seq 1 90); do
    if (exec 3<>/dev/tcp/127.0.0.1/5433) 2>/dev/null; then
        exec 3>&-
        break
    fi
    sleep 1
done

cd src/Anthology/ClientApp
npm run generate >/tmp/factoree-preview-client.log 2>&1
./node_modules/.bin/vite build >>/tmp/factoree-preview-client.log 2>&1
cd "$ROOT"

exec dotnet run --project src/Anthology --no-launch-profile
