#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

export MISE_CONFIG_FILE="$ROOT/.factoree/mise.toml"
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://127.0.0.1:5099
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=anthology;Username=anthology;Password=anthology"

for _ in $(seq 1 90); do
    if (exec 3<>/dev/tcp/127.0.0.1/5433) 2>/dev/null; then
        exec 3>&-
        break
    fi
    sleep 1
done

dotnet build src/Anthology/Anthology.csproj >/tmp/factoree-seed-build.log 2>&1

setsid dotnet run --project src/Anthology --no-launch-profile --no-build --no-restore \
    >/tmp/factoree-seed-app.log 2>&1 &
app_pid=$!

cleanup() {
    kill -- "-$app_pid" 2>/dev/null || true
    wait "$app_pid" 2>/dev/null || true
}
trap cleanup EXIT

ready=0
for _ in $(seq 1 120); do
    code="$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:5099/api/identity/me || true)"
    if [ "$code" = "401" ] || [ "$code" = "200" ]; then
        ready=1
        break
    fi
    sleep 1
done
if [ "$ready" != "1" ]; then
    echo "seed: app did not become ready" >&2
    tail -n 80 /tmp/factoree-seed-app.log >&2 || true
    exit 1
fi

curl -s -o /tmp/factoree-seed-register.json -X POST http://127.0.0.1:5099/api/identity/register \
    -H 'Content-Type: application/json' \
    -d '{"email":"member@example.test","password":"LocalPassword1"}' || true

curl -fsS -c /tmp/factoree-seed-cookies.txt -X POST http://127.0.0.1:5099/api/identity/login \
    -H 'Content-Type: application/json' \
    -d '{"email":"member@example.test","password":"LocalPassword1"}' >/dev/null
curl -fsS -b /tmp/factoree-seed-cookies.txt http://127.0.0.1:5099/api/identity/me >/dev/null
