# Anthology

Self-hostable, event-sourced social media tracker. Films first, then TV, books, games, music.

Also a pragmatic DDD reference codebase in .NET — real product, minimal abstraction.

## Stack

- .NET 10 / C# / EF Core 10
- PostgreSQL 17
- React SPA (Vite)
- Single deployable modular monolith

## Quick start

```bash
docker compose up -d
dotnet run --project src/Anthology
```

App runs at `https://localhost:5001` (or the port shown in console output). Postgres on `localhost:5433`.

## Tests

```bash
dotnet test
```

Tests use Testcontainers — Docker must be running. No manual database setup needed.

## Project structure

```
src/Anthology/
  Program.cs              # composition root
  Kernel/                 # event store, messaging, Result, validation
  Modules/
    Tracking/             # core domain (event-sourced) — track/rate items, diary, library
    Catalog/              # TMDB integration + local title data
    Identity/             # ASP.NET Core Identity (cookie auth)
    Profile/              # user profile (handle, display name, bio)
  Workers/                # background services
  ClientApp/              # React SPA
tests/Anthology.Tests/    # aggregate, integration, convention, endpoint tests
```

## Architecture

- **Event sourcing** for the tracking domain. Aggregates use decide/evolve with pure functions.
- **Vertical slices** — each feature is one file containing command/query, validator, handler, and endpoint.
- **DbContext-per-module** with separate Postgres schemas (`es`, `tracking`, `catalog`, `identity`, `profile`).
- **No mediator, no generic repository, no AutoMapper.** Scrutor decorates a single transaction decorator around command handlers.

## Status

Currently building M1 — the personal spine for film tracking end to end.
