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
    dotnet run --project src/Anthology --urls http://localhost:5199 &
    PID=$!
    sleep 4
    curl -s http://localhost:5199/openapi/v1.json | python3 -m json.tool > src/Anthology/openapi.json
    kill $PID
    echo "OpenAPI spec written to src/Anthology/openapi.json"
