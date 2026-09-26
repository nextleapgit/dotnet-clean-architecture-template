# Clean Architecture API Template (.NET 10)

The base for our backend services: a layered Clean Architecture Web API with multi-tenancy, permissions, audit, a transactional email outbox, and a test suite that enforces the rules. It is packaged as a `dotnet new` template and ships with Claude Code skills that generate code in the same style.

## What's inside

| Area | What you get |
|---|---|
| Architecture | `SharedKernel` → `BuildingBlocks` → `Domain` → `Application` → `Infrastructure` → `Api`, enforced by architecture tests |
| CQRS | Own `ICommand`/`IQuery` abstractions, `ICommandDispatcher`/`IQueryDispatcher`, validation + logging decorators (no MediatR) |
| Errors | `Result`/`Error` everywhere; RFC 9457 problem details with a stable `code` and the request `correlationId` |
| Persistence | EF Core + PostgreSQL behind store interfaces and `IUnitOfWork`; EF Core never leaves Infrastructure |
| Tenancy | Every sign-up gets a tenant; `ICurrentTenantContext` per request; tenant-scoped stores and cache keys |
| Security | JWT access tokens, hashed rotating refresh tokens with reuse detection, logout, per-route permissions, rate limiting |
| Audit | Append-only audit trail (enforced by a database trigger) for authentication and security events |
| Email | Transactional outbox: encrypted payloads, `SKIP LOCKED` leases, retries with jitter, MailKit SMTP, health check |
| API | Minimal APIs under `/api/v1`, OpenAPI + Scalar, health (`/health`, `/health/ready`) |
| Operations | Serilog (+ Seq), OpenTelemetry (OTLP), Docker, Mailpit for local email, GitHub Actions CI |
| Tests | Unit (in-memory fakes), architecture, and integration tests on PostgreSQL + Mailpit via Testcontainers |

## Start a new project

**From GitHub** — click **Use this template**, clone your new repository, then:

```powershell
./scripts/init-project.ps1 -Name Acme.Orders   # renames everything and removes template-only files
```

**With `dotnet new`** — from a clone of this repository or the package attached to each release:

```bash
dotnet new install ./                          # or: dotnet new install CleanArchitecture.Template (from the release feed)
dotnet new ca-api -n Acme.Orders
```

Either way every `CleanArchitecture` becomes `Acme.Orders`. Details: [docs/template.md](docs/template.md).

## Try it

Requirements: .NET 10 SDK (pinned in `global.json`) and Docker.

**Option 1 — everything in Docker (quickest):**

```bash
docker compose up -d --build
./scripts/smoke-test.ps1        # 13 end-to-end checks against the running stack
```

**Option 2 — API on your machine, dependencies in Docker (for debugging):**

```bash
docker compose up -d postgres seq mailpit
dotnet run --project src/CleanArchitecture.Api
```

**Option 3 — step by step:** open `src/CleanArchitecture.Api/CleanArchitecture.Api.http` in Visual Studio, Rider, or VS Code (REST Client) and send the requests in order, or use Scalar.

| URL | What |
|---|---|
| http://localhost:5000/scalar | API reference (Development only) |
| http://localhost:5000/health | Health, including the email backlog check |
| http://localhost:8081 | Seq logs (search by `CorrelationId`) |
| http://localhost:8025 | Mailpit inbox (welcome emails land here) |

Stop with `docker compose down` (add `-v` to also delete the database volume).

Migrations are applied automatically in `Development` only. Elsewhere, apply them as an explicit deployment step — see [docs/architecture.md](docs/architecture.md#migrations).

## Test

```bash
dotnet test --solution CleanArchitecture.slnx   # Docker must be running
```

## Documentation

- [docs/architecture.md](docs/architecture.md) — layers, conventions, and the decisions behind them
- [docs/security.md](docs/security.md) — authentication, sessions, tenancy, permissions, audit
- [docs/email-outbox.md](docs/email-outbox.md) — outbox storage/worker contract and SMTP delivery
- [docs/template.md](docs/template.md) — using and maintaining the template
- [CLAUDE.md](CLAUDE.md) and [.claude/skills](.claude/skills/README.md) — working with Claude Code

Maintained by **AbdulnaserRamadan**. Built on the organization's `dotnet-*` engineering skills.
