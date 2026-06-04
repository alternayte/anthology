# Anthology — convenience commands

# Backend
build:
    dotnet build Anthology.slnx

run:
    dotnet run --project src/Anthology

test:
    dotnet test Anthology.slnx

watch:
    dotnet watch run --project src/Anthology

# Frontend
dev:
    cd src/Anthology/ClientApp && npm run dev

install-ui:
    cd src/Anthology/ClientApp && npm install

generate-api:
    cd src/Anthology/ClientApp && npx @hey-api/openapi-ts

# Infrastructure
db:
    docker compose up -d

db-down:
    docker compose down

# Full stack
up: db
    dotnet run --project src/Anthology

export-openapi:
    #!/usr/bin/env bash
    ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Anthology --no-launch-profile --urls http://localhost:5199 &
    PID=$!
    sleep 6
    curl -s http://localhost:5199/openapi/v1.json > src/Anthology/openapi.json
    kill $PID
    echo "OpenAPI spec written to src/Anthology/openapi.json"
