#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export ASPNETCORE_URLS=http://127.0.0.1:5099
export ConnectionStrings__DefaultConnection="${ConnectionStrings__DefaultConnection:-Host=localhost;Port=5433;Database=anthology;Username=anthology;Password=anthology}"

dotnet run --project src/Anthology --no-launch-profile >.factoree/seed-app.log 2>&1 &
app_pid=$!
trap 'kill "$app_pid" 2>/dev/null || true; wait "$app_pid" 2>/dev/null || true' EXIT

ready=0
for _ in $(seq 1 120); do
  code=$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:5099/api/identity/me || true)
  if [ "$code" = "401" ] || [ "$code" = "200" ]; then
    ready=1
    break
  fi
  sleep 1
done
if [ "$ready" != "1" ]; then
  echo "Anthology did not start within 120 seconds" >&2
  tail -n 50 .factoree/seed-app.log >&2 || true
  exit 1
fi

register_status=$(curl -s -o .factoree/seed-register.json -w '%{http_code}' \
  -H 'Content-Type: application/json' \
  -d '{"email":"member@example.test","password":"local-password"}' \
  http://127.0.0.1:5099/api/identity/register)

if [ "$register_status" != "200" ]; then
  curl -fsS -o /dev/null \
    -H 'Content-Type: application/json' \
    -d '{"email":"member@example.test","password":"local-password"}' \
    http://127.0.0.1:5099/api/identity/login
fi
