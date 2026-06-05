# CLAUDE.md

## What is this project

Anthology is a self-hostable, event-sourced social media tracker (films first, then TV, books, games, music). It doubles as a pragmatic DDD reference codebase in .NET — the product is real, but its primary purpose is demonstrating how to build and evolve a lean event-sourced system without over-abstraction.

## Stack

- .NET 10 / C# / EF Core 10
- PostgreSQL (single datastore, no Kafka/Redis/etc.)
- React SPA (Vite, built to wwwroot, served by the .NET host)
- Single deployable modular monolith

## Project structure

```
Anthology.slnx                    # solution file (repo root)
src/
  Anthology/                      # the single runnable project
    Program.cs                    # composition root
    Kernel/                       # shared building blocks (event store, messaging, Result, ValidationFilter)
    Modules/
      Tracking/                   # core domain (event-sourced) — TrackedItem aggregate, diary/library projections
      Catalog/                    # supporting — TMDB integration + local Title reference data
      Identity/                   # supporting — ASP.NET Core Identity (cookie/BFF, own schema)
      Profile/                    # supporting — handle, display name, avatar, bio
    Workers/                      # BackgroundService hosts (empty in M1)
    ClientApp/                    # React SPA
tests/
  Anthology.Tests/                # aggregate, integration, convention, endpoint tests
docs/                             # public docs (internal/ is gitignored)
docker-compose.yml                # Postgres only
```

## Architecture rules — follow these strictly

- **Vertical slices in a modular monolith.** A slice is ONE file containing the command/query, validator, handler, and endpoint mapping as nested types in a static class.
- **Modules are flat until they hurt.** No sub-folders until ~10-12 files makes the folder hard to scan. No speculative nesting.
- **No mediator.** Plain handler classes implementing `ICommandHandler<TCommand, TResult>`. Cross-cutting is ONE Scrutor-decorated transaction decorator — not a pipeline.
- **No over-abstraction.** No generic `IRepository<T>` over EF, no four-project Clean Architecture split, no AutoMapper, no interface-per-class "for swappability", no port/adapter wrapper around Postgres.
- **Event-source only what is event-shaped.** Tracking and Social are event-sourced. Catalog is relational reference data. Identity is relational, not event-sourced.
- **Typed throughout.** Events are records implementing `IDomainEvent`. No `object`/`dynamic` in domain or handler code. The only untyped hop is the `(event_type text, payload jsonb)` row boundary in the serializer.
- **Aggregate pattern: decide/evolve.** Pure functions. `Decide` returns `Result<IReadOnlyList<IDomainEvent>>`. `Evolve` folds events into state.
- **Deterministic stream IDs.** UUIDv5 from `userId + titleId` — no lookup table.
- **Validation:** aggregate invariants in `Decide` (state-dependent). Input validation at the API edge via FluentValidation through a reusable `ValidationFilter<T>` endpoint filter. Value objects carry rules the type system can enforce.
- **Error handling:** `Result<T>` with typed `ErrorKind` for expected outcomes, exceptions for bugs/infra. ProblemDetails everywhere via `ToHttpResult()` + global `IExceptionHandler`.
- **Integration events ≠ domain events.** The outbox holds integration events (public, versioned, CloudEvents-shaped, on a separate version clock). Internal domain events never leak.
- **DbContext-per-module** with separate Postgres schemas and per-context migrations.
- **Minimal APIs only.** No controllers. `MapGroup` per context. `TypedResults` + `Results<...>` union returns for accurate OpenAPI.
- **Auth: cookie-based, same-origin (BFF).** The SPA never handles tokens. ASP.NET Core Identity in its own module. No PII in events.

## Milestones

Currently building **M1 — Personal Spine (Film)**: the ES loop end to end. Auth, minimal React UI, add a film from TMDB, track/rate items, personal diary, outbox/inbox tables wired.

## Build & run

```bash
dotnet build Anthology.slnx
dotnet run --project src/Anthology
# docker compose up    # for Postgres
```

## Testing

```bash
dotnet test
```

Test categories: aggregate given/when/then tests (pure, fast), Testcontainers Postgres integration tests, convention tests (choke-point guardrails), endpoint tests, query/keyset-pagination tests.

## Code style

- Slices are static classes with nested types (Command, Validator, Handler, endpoint Map method)
- Prefer records for DTOs, events, value objects
- camelCase JSON properties, enums as lower snake_case strings
- `AsNoTracking` for all read queries
- Keyset (cursor) pagination, not offset
- No comments unless the WHY is non-obvious
