#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

export MISE_CONFIG_FILE="$root/.factoree/mise.toml"
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://127.0.0.1:5018

log="$(mktemp)"
dotnet run --project src/Anthology --no-launch-profile >"$log" 2>&1 &
app_pid=$!

cleanup() {
  kill "$app_pid" 2>/dev/null || true
  wait "$app_pid" 2>/dev/null || true
  rm -f "$log"
}
trap cleanup EXIT

ready=0
for _ in $(seq 1 300); do
  status="$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:5018/api/identity/me || true)"
  if [ -n "$status" ] && [ "$status" != "000" ]; then
    ready=1
    break
  fi
  sleep 1
done

if [ "$ready" != "1" ]; then
  echo "Anthology did not start for seeding" >&2
  tail -n 100 "$log" >&2 || true
  exit 1
fi

payload='{"email":"member@example.test","password":"Local-password1"}'
response="$(mktemp)"
register_status="$(curl -sS -o "$response" -w '%{http_code}' \
  -H 'Content-Type: application/json' -d "$payload" \
  http://127.0.0.1:5018/api/identity/register || true)"

if [ "$register_status" != "200" ]; then
  login_status="$(curl -sS -o /dev/null -w '%{http_code}' \
    -H 'Content-Type: application/json' -d "$payload" \
    http://127.0.0.1:5018/api/identity/login || true)"
  if [ "$login_status" != "200" ]; then
    echo "Could not create or sign in the member account (register $register_status, login $login_status)" >&2
    cat "$response" >&2 || true
    rm -f "$response"
    exit 1
  fi
fi

rm -f "$response"
