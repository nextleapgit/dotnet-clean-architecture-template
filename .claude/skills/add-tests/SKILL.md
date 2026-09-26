---
name: add-tests
description: Backfill missing tests for existing use cases in the Clean Architecture template — handler unit tests with in-memory fakes, validator tests, tenant-isolation tests, and HTTP integration tests. Use when the user asks to add, improve, or backfill test coverage.
argument-hint: <use case or feature to cover, e.g. "CopyTodoCommand" or "the Users feature">
---

# Add Tests for an Existing Use Case

Backfill the test types this template expects for every slice. Read the handler, validator, store, and endpoint first, then mirror the closest existing test class.

## Workflow

1. **List every outcome** of the handler: each `return Result.Failure(...)`, the other-tenant / other-user case for tenant-owned data, and the happy path (state, domain event, save count, audit record, queued email).
2. **Check existing tests** in `tests/CleanArchitecture.UnitTests/{Feature}/` and `tests/CleanArchitecture.IntegrationTests/{Feature}/`; extend, don't duplicate.
3. **Unit tests** with the fakes in `tests/CleanArchitecture.UnitTests/Fakes/` — never a database, never mocking the store with NSubstitute when an in-memory fake exists. Substitute only simple interfaces (`IPasswordHasher`).
4. **Validator tests** — one failing test per rule, one valid command.
5. **Integration tests** over real HTTP: happy path verified by a follow-up request, error translation (status and `code`), and isolation between two registered accounts (two tenants).
6. **Run** `dotnet test --solution CleanArchitecture.slnx` (Docker required) and fix failures before finishing.

## Conventions

- Class names `{Handler}Tests`, `{Feature}ValidatorsTests`, `{Feature}Tests`; methods `Handle_Should_{Outcome}_When{Condition}` (unit) and `{Action}_Should_{Outcome}[_When{Condition}]` (integration).
- `// Arrange` / `// Act` / `// Assert` in every test; `TestContext.Current.CancellationToken` for every token.
- Assert exact errors (`result.Error.ShouldBe(TodoItemErrors.NotFound(id))`) and events by value (`DomainEvents.ShouldContain(new XDomainEvent(id))`).
- Database-level tests (constraints, locking, audit immutability) belong in the integration project; each test owns and cleans up only the rows it creates.

Templates: [../add-feature/references/tests.md](../add-feature/references/tests.md).
