#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="http://0.0.0.0:3000"
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=anthology;Username=anthology;Password=anthology"

export Tmdb__BaseUrl="http://localhost:18081/3"
export Tmdb__ImageBaseUrl="http://localhost:18081/t/p"
export OpenLibrary__BaseUrl="http://localhost:18082"
export OpenLibrary__CoverBaseUrl="http://localhost:18082/covers"
export Igdb__BaseUrl="http://localhost:18083/v4/"
export Igdb__TokenUrl="http://localhost:18083/oauth2/token"
export Igdb__ClientId="local-fake"
export Igdb__ClientSecret="local-fake"
export Igdb__ImageBaseUrl="http://localhost:18083/images/igdb/image/upload"
export MusicBrainz__BaseUrl="http://localhost:18084/ws/2"
export MusicBrainz__CoverBaseUrl="http://localhost:18084/release-group"
export Embedding__BaseUrl="http://localhost:18085/v1"
export Embedding__ApiKey="local-fake"
export VITE_TMDB_IMAGE_BASE="http://localhost:18081/t/p"

wait_port() {
  local port=$1
  for _ in $(seq 1 180); do
    if (echo > "/dev/tcp/127.0.0.1/${port}") >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  echo "port ${port} did not become ready" >&2
  return 1
}

wait_port 5433
wait_port 18081
wait_port 18082
wait_port 18083
wait_port 18084
wait_port 18085

cd src/Anthology/ClientApp
npm run generate
npx vite build
cd ../../..

exec dotnet run --project src/Anthology --no-launch-profile
