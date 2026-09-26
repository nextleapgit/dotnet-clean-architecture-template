# CLAUDE.md

Layered Clean Architecture Web API template for .NET 10 on PostgreSQL: multi-tenant, permission-guarded, audited, with a transactional email outbox. It is published as a `dotnet new` template (`ca-api`), so `CleanArchitecture` in names is a placeholder that becomes the product name. Read `docs/architecture.md` before structural changes.

## Commands

```bash
dotnet build CleanArchitecture.slnx                # warnings are errors (.NET analyzers + SonarAnalyzer)
dotnet test --solution CleanArchitecture.slnx      # Microsoft.Testing.Platform (xUnit v3); Docker needed for integration tests
dotnet test --project tests/CleanArchitecture.UnitTests        # fast, no Docker
dotnet tool restore                                # pinned dotnet-ef (dotnet-tools.json)
dotnet ef migrations add <Name> --project src/CleanArchitecture.Infrastructure --startup-project src/CleanArchitecture.Api --output-dir Database/Migrations

docker compose up -d --build                       # full stack: API :5000, Seq :8081, Mailpit :8025, Postgres :5432
docker compose up -d postgres seq mailpit          # dependencies only, then: dotnet run --project src/CleanArchitecture.Api
./scripts/smoke-test.ps1                           # end-to-end check against a running stack
dotnet pack template-pack -o artifacts             # template package
```

`src/CleanArchitecture.Api/CleanArchitecture.Api.http` walks through every endpoint. Scalar (Development only): http://localhost:5000/scalar.

## Map

| Project | Holds |
|---|---|
| `SharedKernel` | `Result`, `Error`/`ErrorType`, `Entity`, `IDomainEvent`, `TenantId`, `IDateTimeProvider` |
| `BuildingBlocks` | CQRS (`ICommand`, `IQuery`, handlers, `ICommandDispatcher`/`IQueryDispatcher`, `AddCqrs`, validation + logging decorators), `ICurrentTenantContext`, `IUnitOfWork`, `IAuditLog`/`AuditRecord`, email ports (`IEmailOutbox`, `IEmailSender`, `EmailMessage`, `EmailErrorCodes`), persistence exceptions |
| `Domain` | `Tenant`, `User`, `RefreshToken`, `TodoItem` (sample) — behavior methods, events, `{Entity}Errors` |
| `Application` | One folder per use case; store interfaces (`IUserStore`, `IRefreshTokenStore`, `ITenantStore`, `ITodoItemStore`); `UserAuditActions`; `WelcomeEmail` |
| `Infrastructure` | `ApplicationDbContext`, stores, `UnitOfWork`, JWT (`TokenProvider`, `JwtOptions`), `PasswordHasher`, permissions, `CurrentTenantContext`, `AuditLog`, email outbox (`Email/`), migrations |
| `Api` | Endpoints (`Endpoints/{Feature}`), `CustomResults`, `GlobalExceptionHandler`, `CorrelationIdMiddleware`, OpenAPI + Scalar, rate limiting, OpenTelemetry |
| `tests/*UnitTests` | Handlers, domain, building blocks with in-memory fakes (`Fakes/`) — no database |
| `tests/*ArchitectureTests` | Layer, persistence-isolation, forbidden-dependency, and convention rules |
| `tests/*IntegrationTests` | HTTP + PostgreSQL + Mailpit (Testcontainers): users, sessions, tenancy, audit, API conventions, outbox, SMTP |

## Features and where they live

- **Tenancy** — each registration creates a `Tenant`; JWT carries `tenant_id`; `ICurrentTenantContext` is per request. Stores take `TenantId`; other tenants' data is `NotFound`; cache keys include the tenant. Extension point for hierarchies: `AccessibleTenantIds`.
- **Authentication and sessions** — PBKDF2 passwords; JWT access tokens; refresh tokens stored as SHA-256 hashes, rotated within a family. Replaying a *rotated* token revokes the family and writes a Critical audit entry (a token revoked by logout is just invalid). `xmin` concurrency → 409 on double rotation. `POST users/logout`.
- **Permissions** — every endpoint `.HasPermission(Permissions.X)` except anonymous register/login/refresh. `PermissionProvider` grants a default set; replace it with a role model per product.
- **Errors** — RFC 9457 problem details with stable `code` and `correlationId`. Concurrency and unique-constraint races → 409 (`General.ConcurrencyConflict`, `General.UniqueConstraintViolation`).
- **Audit** — `IAuditLog.Record` in the same unit of work; enriched with tenant, user, correlation id, IP, user agent, time. `audit_entries` is append-only via a DB trigger in `InitialCreate` (hand-written block — keep it when regenerating).
- **Email outbox** — `IEmailOutbox.EnqueueAsync` inside `IUnitOfWork.BeginTransactionAsync`; encrypted payload (Data Protection, keys in DB); worker claims with `FOR UPDATE SKIP LOCKED` and leases; retries with jitter; settlement and cleanup; MailKit SMTP; `email-outbox` health check (degraded-only). Worker is off by default, on in Development (Mailpit).
- **Operations** — correlation id middleware (safe client ids only), Serilog (+ Seq in Development), OpenTelemetry via `OTEL_EXPORTER_OTLP_ENDPOINT`, `/health` and `/health/ready`, rate limiting (global + authentication policy). Migrations run at start-up in Development only.

## Configuration

| Section | Notes |
|---|---|
| `ConnectionStrings:Database` | PostgreSQL |
| `Jwt` | `Secret` ≥ 32 chars (validated at start-up), `Issuer`, `Audience`, `ExpirationInMinutes`. Secrets via user secrets / environment |
| `RateLimiting` | `Global` and `Authentication` permit limits and windows |
| `EmailOutbox` | `Enabled` (default false), polling, attempts, lease, retry, retention, health threshold — validated at start-up |
| `Smtp` | Host, port, `SecurityMode` (TLS required outside Development), sender, optional credentials, timeout |

## Project skills

`.claude/skills`: `add-feature` (new use case), `add-entity` (new table), `add-tests`, `ca-review` (before committing). Organization skills (`dotnet-*`, `dotnet-audit`, `transactional-email-outbox`) also apply; deliberate deviations are in `docs/architecture.md`.

## Rules the build and tests enforce

- Dependencies: Domain → SharedKernel; BuildingBlocks → SharedKernel; Application → Domain + BuildingBlocks; Infrastructure → Application; Api composes. No EF Core outside Infrastructure; endpoints never touch stores or the DbContext.
- No MediatR, no Swashbuckle (Scalar is the documentation UI).
- Handlers, validators, and stores are `internal sealed`; handlers use `HandleAsync` and are found by `AddCqrs`; endpoints call dispatchers only.
- Entities have no public setters; changes go through entity methods that raise domain events.
- Every endpoint is under `/api/v1` and has a known permission or is one of the anonymous sign-in routes.

## Rules to follow by judgement

- Tenant and user come from `ICurrentTenantContext`, never from requests.
- Expected failures return `Result.Failure(...)` with `{Entity}Errors`; never throw for business rules.
- Audit sensitive actions; never log or audit passwords, tokens, secrets, or email contents.
- Use cases never call `IEmailSender`; HTML-encode untrusted values in email bodies.
- Use `IDateTimeProvider` instead of `DateTime.UtcNow` in application code.
- Every new behavior ships with unit tests (fakes), validator tests, and integration tests including tenant isolation.
- Production never auto-migrates; ship migration bundles or scripts as a deployment step.
