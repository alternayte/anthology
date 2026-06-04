# Anthology — convenience commands

# Backend
build:
    dotnet build Anthology.slnx

run:
    ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Anthology --no-launch-profile

test:
    dotnet test Anthology.slnx

watch:
    ASPNETCORE_ENVIRONMENT=Development dotnet watch run --project src/Anthology --no-launch-profile

# Database
db:
    docker compose up -d

db-down:
    docker compose down

migrate:
    #!/usr/bin/env bash
    cd src/Anthology
    dotnet ef database update --context EventStoreDbContext
    dotnet ef database update --context Anthology.Modules.Identity.IdentityDbContext
    dotnet ef database update --context CatalogDbContext
    dotnet ef database update --context TrackingDbContext
    dotnet ef database update --context ProfileDbContext
    echo "All migrations applied."

add-migration name:
    #!/usr/bin/env bash
    cd src/Anthology
    dotnet ef migrations add {{name}} --context EventStoreDbContext --output-dir Kernel/EventStore/Migrations
    dotnet ef migrations add {{name}} --context Anthology.Modules.Identity.IdentityDbContext --output-dir Modules/Identity/Migrations
    dotnet ef migrations add {{name}} --context CatalogDbContext --output-dir Modules/Catalog/Migrations
    dotnet ef migrations add {{name}} --context TrackingDbContext --output-dir Modules/Tracking/Migrations
    dotnet ef migrations add {{name}} --context ProfileDbContext --output-dir Modules/Profile/Migrations

# Frontend
dev:
    cd src/Anthology/ClientApp && npm run dev

install-ui:
    cd src/Anthology/ClientApp && npm install

generate-api:
    cd src/Anthology/ClientApp && npx @hey-api/openapi-ts

# OpenAPI — generates the spec at build time without running the server
export-openapi:
    #!/usr/bin/env bash
    dotnet build src/Anthology/Anthology.csproj
    rm -f src/Anthology/obj/Anthology.OpenApiFiles.cache
    dotnet msbuild src/Anthology/Anthology.csproj -t:GenerateOpenApiDocuments /p:OpenApiGenerateDocuments=true
    mv src/Anthology/Anthology.json src/Anthology/openapi.json
    echo "OpenAPI spec written to src/Anthology/openapi.json"

# Full stack
up: db
    ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Anthology --no-launch-profile
