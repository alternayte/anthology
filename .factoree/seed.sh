#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="http://127.0.0.1:5099"
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=anthology;Username=anthology;Password=anthology"

for _ in $(seq 1 180); do
  if (echo > /dev/tcp/127.0.0.1/5433) >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

dotnet run --project src/Anthology --no-launch-profile >/tmp/anthology-seed.log 2>&1 &
pid=$!
trap 'kill "$pid" 2>/dev/null || true; wait "$pid" 2>/dev/null || true' EXIT

ready=0
for _ in $(seq 1 240); do
  code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 2 http://127.0.0.1:5099/api/identity/me || true)
  if [ -n "$code" ] && [ "$code" != "000" ]; then
    ready=1
    break
  fi
  sleep 1
done
if [ "$ready" != "1" ]; then
  cat /tmp/anthology-seed.log
  exit 1
fi

register_status=$(curl -sS -o /tmp/anthology-register.out -w '%{http_code}' --max-time 30 \
  -H 'Content-Type: application/json' \
  -d '{"email":"member@example.test","password":"Local-password1"}' \
  http://127.0.0.1:5099/api/identity/register || true)
if [ "$register_status" != "200" ] && [ "$register_status" != "400" ]; then
  cat /tmp/anthology-register.out
  exit 1
fi

login_status=$(curl -sS -o /tmp/anthology-login.out -w '%{http_code}' --max-time 30 \
  -H 'Content-Type: application/json' \
  -d '{"email":"member@example.test","password":"Local-password1"}' \
  http://127.0.0.1:5099/api/identity/login || true)
if [ "$login_status" != "200" ]; then
  cat /tmp/anthology-login.out
  exit 1
fi
