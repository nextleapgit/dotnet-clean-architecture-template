---
name: ca-review
description: Review pending changes against the Clean Architecture template's conventions — layer boundaries, persistence isolation, tenancy, permissions, Result-based errors, audit, email outbox, slice structure, and test coverage. Use when the user asks to review changes, check conventions, or audit a feature before committing.
argument-hint: [optional: specific files or feature to review; defaults to the working-tree diff]
---

# Clean Architecture Convention Review

Review the given scope (default: `git diff` + untracked files) against this template's conventions. Report findings with file:line references, ordered by severity. Do not fix anything unless asked.

## Checklist

### Layers and persistence (blockers)
- Domain references only SharedKernel; BuildingBlocks only SharedKernel; Application only Domain + BuildingBlocks; nothing references Api.
- No `Microsoft.EntityFrameworkCore` outside Infrastructure (and tests). Application uses store interfaces and `IUnitOfWork` only; endpoints never touch stores or the DbContext.
- No MediatR; no Swagger UI (Scalar is the documentation UI).
- Production start-up never migrates; migrations are reviewed for tenant ids, indexes, uniqueness, delete behavior, nullability.

### Tenancy and security (blockers)
- Tenant-owned entities carry `TenantId`; every store read filters by the tenant passed from `ICurrentTenantContext` — never from the request. Other tenants' data surfaces as `NotFound`.
- Cache keys include the tenant.
- Every endpoint is under `/api/v1` and has `.HasPermission(...)`, or is a deliberate `.AllowAnonymous()` sign-in route.
- Secrets, tokens, and passwords are never logged, audited, or stored in plain text (refresh tokens are stored hashed).
- Sensitive actions (authentication, permissions, tenant changes, security events) record an audit entry in the same unit of work; audit rows are never updated or deleted.
- Emails are enqueued with `IEmailOutbox` inside an explicit transaction together with the state change; no use case calls `IEmailSender`; untrusted values are HTML-encoded.

### Slice structure and errors
- One folder per use case; `internal sealed` handlers with `HandleAsync`, discovered by `AddCqrs`; endpoints dispatch through `ICommandDispatcher`/`IQueryDispatcher`.
- State changes go through entity methods/factories (private setters) that enforce invariants and raise events.
- Expected failures return `Result` with `{Entity}Errors` codes (`Feature.Reason`) and the right `ErrorType`; no exceptions for control flow.
- Validators exist for inputs; lengths come from entity constants; time rules use `IDateTimeProvider`.

### Tests
- Unit tests for every failure path (including other tenant/user) and the happy path, with in-memory fakes.
- Validator tests per rule; integration tests over HTTP including tenant isolation for tenant-owned data.
- Build is warning-free and `dotnet test --solution CleanArchitecture.slnx` passes.

## Output format

Group findings as **Blockers**, **Convention violations**, and **Test gaps**. For each: `file:line`, what's wrong, and the one-line fix. Close with a verdict: ready to commit, or what must change first.
