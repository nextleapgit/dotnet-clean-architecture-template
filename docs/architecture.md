# Architecture

## Projects

```
src/
  CleanArchitecture.SharedKernel     Result, Error, Entity, IDomainEvent, TenantId, IDateTimeProvider
  CleanArchitecture.BuildingBlocks   CQRS + dispatchers + decorators, tenancy, unit of work, audit and email ports
  CleanArchitecture.Domain           Entities with behavior, domain events, error catalogs
  CleanArchitecture.Application      Use cases (one folder each), store interfaces, validators
  CleanArchitecture.Infrastructure   EF Core, stores, JWT, permissions, tenant context, audit log, email outbox
  CleanArchitecture.Api              Minimal API endpoints, middleware, OpenAPI/Scalar, composition root
tests/
  CleanArchitecture.UnitTests          Handlers, domain, building blocks — in-memory fakes, no database
  CleanArchitecture.ArchitectureTests  Dependency, persistence, and convention rules
  CleanArchitecture.IntegrationTests   HTTP + PostgreSQL + Mailpit via Testcontainers
```

Dependencies point one way: `Domain → SharedKernel`, `BuildingBlocks → SharedKernel`, `Application → Domain + BuildingBlocks`, `Infrastructure → Application`, `Api → Infrastructure`. `SharedKernel`, `BuildingBlocks`, `Domain`, and `Application` never reference EF Core. All of this is checked by `CleanArchitecture.ArchitectureTests`.

## Request flow

```
HTTP → CorrelationIdMiddleware → exception handler → authentication → authorization (permission policy)
     → endpoint (map request) → ICommandDispatcher / IQueryDispatcher
     → LoggingDecorator → ValidationDecorator → handler
     → stores / IUnitOfWork / IAuditLog / IEmailOutbox → Result → problem details or 2xx
```

## Use cases

Each use case is a folder under `Application/{Feature}/{UseCase}` with a command or query, an `internal sealed` handler (`HandleAsync`), and a validator. `AddCqrs` registers handlers and validators by assembly scanning as scoped services and wraps them with validation (commands and queries) and logging. Endpoints resolve only the dispatchers.

Handlers return `Result`/`Result<T>`. Expected failures use errors from `{Entity}Errors` with stable codes (`Feature.Reason`); `CustomResults.Problem` maps the error type to the status code and writes `code`, `detail`, and `correlationId`. Unexpected exceptions become a 500 with `code = General.ServerFailure`; optimistic concurrency conflicts become a 409 with `General.ConcurrencyConflict`.

## Domain

Entities have private setters, a private constructor for EF Core, a `Create` factory, and behavior methods (`Complete`, `Copy`, `Rotate`, ...) that enforce invariants and raise domain events. Domain events are published after `SaveChanges`; inside an explicit transaction they run before the commit, each in its own DI scope.

## Persistence

Application code sees store interfaces (`ITodoItemStore`, `IUserStore`, ...) and `IUnitOfWork`. Infrastructure implements them with EF Core (`ApplicationDbContext`, snake_case naming, `TenantId` value conversion by convention). Queries project straight into response DTOs inside the store.

### Migrations

- One `InitialCreate` migration ships with the template; add new migrations with the pinned `dotnet-ef` tool.
- The `InitialCreate` migration contains a hand-written block that makes `audit_entries` append-only. Keep it.
- `Development` applies migrations at start-up. Other environments must not: generate a bundle (`dotnet ef migrations bundle`) or an idempotent script and run it as an explicit, observable deployment step.

## Observability

Serilog (console everywhere, Seq in Development) with the correlation id on every log event; OpenTelemetry traces and metrics exported via OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; `/health` (all checks, including a degraded-only email backlog check) and `/health/ready` (database only, for load balancers).

## Decisions and deviations from the organization skills

The template follows the `dotnet-*` skills except where noted here; each deviation was a deliberate choice for a general-purpose starting point.

| Topic | Skill says | Template does | Why |
|---|---|---|---|
| System shape | Modular monolith, four projects per module | One set of layers (improved layered architecture) | Chosen as the default for new projects; split into modules when a product needs it |
| Tenant hierarchy | Owner → MainProvider → SubProvider → Customer | One flat tenant per sign-up; `AccessibleTenantIds` is the extension point for a hierarchy | The hierarchy is specific to the tracking platform |
| Permissions | Role/permission model with delegation rules | Default permission set for every authenticated user (`PermissionProvider`) | No role model is assumed; replace `PermissionProvider` when one exists |
| Handler registration | Explicit `AddScoped` per handler | Assembly scanning in `AddCqrs` (still scoped) | Fewer merge conflicts, decorators applied uniformly |
| Persistence tests | SQLite | PostgreSQL via Testcontainers | The outbox relies on `FOR UPDATE SKIP LOCKED`, partial indexes, and triggers that SQLite does not have |
| Error body | `{ code, message, correlationId }` | RFC 9457 problem details with `code`, `detail` (the message), and `correlationId` | Standard format that still carries the required fields |
| Tenant context fields | Also Permissions, EnabledModules, Culture, TimeZone | Tenant, user, accessible tenants | Add fields when the corresponding features exist |
