---
name: add-feature
description: Scaffold a complete Clean Architecture feature slice — command or query, handler, FluentValidation validator, store method, versioned minimal API endpoint with a permission, and tests (unit, validator, integration, tenant isolation). Use when the user asks to add a feature, use case, command, query, or endpoint to this template.
argument-hint: <feature description, e.g. "archive a todo item" or "get todos due this week">
---

# Add a Feature (Vertical Slice)

Scaffold a full use case: an Application-layer command/query with its handler, the store method it needs, a `/api/v1` minimal-API endpoint, and tests. No MediatR — this code base uses its own `ICommand`/`IQuery` abstractions from `CleanArchitecture.BuildingBlocks.Cqrs`, dispatched by `ICommandDispatcher`/`IQueryDispatcher`.

## Workflow

1. **Classify the use case.** A state change is a **command**; a read is a **query**. Name it verb + entity: `ArchiveTodoCommand`, `GetOverdueTodosQuery`.
2. **Check the Domain.** The behavior belongs on the entity (private setters): add a method such as `Archive(DateTime)` that enforces the rule, raises the domain event, and returns `Result`. New entity? Use the `add-entity` skill first.
3. **Add the store method** the handler needs to the store interface in `src/CleanArchitecture.Application/{Feature}/I{Entity}Store.cs`, implement it in `src/CleanArchitecture.Infrastructure/{Feature}/{Entity}Store.cs`, and mirror it in the unit-test fake `tests/CleanArchitecture.UnitTests/Fakes/InMemory{Entity}Store.cs`. Every read takes the `TenantId` (and owner where applicable).
4. **Create the Application slice** in `src/CleanArchitecture.Application/{Feature}/{UseCase}/`: command/query, handler, validator. Templates: [references/command-slice.md](references/command-slice.md), [references/query-slice.md](references/query-slice.md).
5. **Create the endpoint** in `src/CleanArchitecture.Api/Endpoints/{Feature}/{UseCase}.cs` with an explicit permission. Template: [references/endpoint.md](references/endpoint.md).
6. **Write tests** — handler unit tests (including another tenant's data), validator tests, integration tests. Templates: [references/tests.md](references/tests.md).
7. **Verify:** `dotnet build CleanArchitecture.slnx` then `dotnet test --solution CleanArchitecture.slnx`. Build warnings are errors; architecture tests must pass.

## Non-negotiable conventions

- **Folder = use case** under `src/CleanArchitecture.Application/{Feature}/`.
- **Handlers are `internal sealed`**, primary constructors, `HandleAsync(command, cancellationToken)`, returning `Result`/`Result<T>`. They are discovered by `AddCqrs` assembly scanning — no manual registration, and endpoints never inject handlers directly.
- **No EF Core in Application.** Data access only through store interfaces and `IUnitOfWork` (`SaveChangesAsync`, `BeginTransactionAsync`). An architecture test fails the build otherwise.
- **Tenant scope on every read.** Take the tenant and user from `ICurrentTenantContext` — never from the request. A resource of another tenant or user is reported as `NotFound`.
- **Expected failures return `Result.Failure`** with an error from `{Entity}Errors` (`"{Feature}.{Reason}"` code, correct `ErrorType`). Never throw for business rules.
- **Validation** lives in a `{UseCase}Validator` (`internal sealed`, FluentValidation). The decorator validates commands *and* queries before the handler runs. Length limits come from entity constants; time rules use `IDateTimeProvider`.
- **Sensitive actions are audited** with `IAuditLog.Record(...)` in the same unit of work (authentication, permission, tenant, and security-relevant changes — see the `dotnet-audit` skill). Action names are stable constants.
- **Emails go through `IEmailOutbox.EnqueueAsync`** inside `IUnitOfWork.BeginTransactionAsync`, committed with the state change. Never call `IEmailSender`. HTML-encode user input in bodies.
- **Caching:** hot reads may use `HybridCache` with keys from `{Feature}CacheKeys` that include the tenant; every mutating command removes the affected key.
- **Endpoints** are thin: map request → command, dispatch, `result.Match(...)`. `.WithTags(Tags.X)` and `.HasPermission(Permissions.X)` on every route; only sign-in style routes use `.AllowAnonymous()`.

## Naming reference

| Artifact | Pattern | Example |
|---|---|---|
| Command | `{Verb}{Entity}Command` | `ArchiveTodoCommand` |
| Query | `Get{X}Query` | `GetOverdueTodosQuery` |
| Handler | `{Command/Query}Handler` | `ArchiveTodoCommandHandler` |
| Validator | `{Command/Query}Validator` | `ArchiveTodoCommandValidator` |
| Response | `{X}Response` in the feature folder | `TodoResponse` |
| Endpoint | `Endpoints/{Feature}/{UseCase}.cs` | `Endpoints/Todos/Archive.cs` |
| Permission | `resource:action` in `Permissions` | `todos:write` |
| Audit action | `resource.past_tense` in `{Feature}AuditActions` | `users.registered` |
| Unit test | `{Handler}Tests` | `ArchiveTodoCommandHandlerTests` |
| Test method | `Handle_Should_{Outcome}_When{Condition}` | `Handle_Should_ReturnNotFound_WhenTodoBelongsToAnotherTenant` |
