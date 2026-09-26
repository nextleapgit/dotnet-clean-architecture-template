# Clean Architecture Agent Skills for Claude Code

Project skills that teach Claude Code this template's conventions, so every feature it builds matches the code base: vertical-slice use cases dispatched through `ICommandDispatcher`/`IQueryDispatcher` (no MediatR), store interfaces instead of EF Core in Application, tenant-scoped data, per-route permissions under `/api/v1`, audit for sensitive actions, the transactional email outbox, and full test coverage.

They build on the organization-wide `dotnet-*` skills (`dotnet-architecture`, `dotnet-cqrs-results`, `dotnet-minimal-api`, `dotnet-persistence`, `dotnet-multitenancy`, `dotnet-testing`, `dotnet-audit`, `transactional-email-outbox`). Where this template deliberately differs from them, `docs/architecture.md` lists the decision.

| Skill | Invoke with | What it does |
|---|---|---|
| **add-feature** | `/add-feature archive a todo item` | Scaffolds a vertical slice: command/query, handler, validator, store method, endpoint, and unit + validator + integration tests. |
| **add-entity** | `/add-entity Project with a name and owner` | Adds a tenant-owned domain entity end to end: entity, errors, events, store, EF configuration, migration, test fakes. |
| **add-tests** | `/add-tests CopyTodoCommand` | Backfills handler, validator, tenant-isolation, and integration tests for existing use cases. |
| **ca-review** | `/ca-review` | Reviews pending changes against the conventions: layers, tenancy, permissions, results, audit, outbox, tests. |

Claude picks the right skill automatically when you say things like "add an endpoint to snooze a todo".

## Try it

```
/add-feature snooze a todo until a given date
```
