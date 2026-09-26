# Query Slice Templates

Files go in `src/CleanArchitecture.Application/{Feature}/{UseCase}/`. Queries never change state.

## Query

```csharp
using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Todos.GetOverdue;

public sealed record GetOverdueTodosQuery : IQuery<List<TodoResponse>>;
```

## Response DTO

One flat response per feature (`src/CleanArchitecture.Application/{Feature}/{X}Response.cs`), shared by that feature's queries and projected by the store. Never return domain entities.

## Store method

Add to the store interface — tenant and owner are explicit parameters:

```csharp
Task<List<TodoResponse>> ListOverdueAsync(TenantId tenantId, Guid userId, DateTime utcNow, CancellationToken cancellationToken);
```

Implement it in the Infrastructure store on top of its single scoping helper (`Owned(tenantId, userId)`) and project with `Select` straight into the DTO. Mirror it in the in-memory fake used by unit tests.

## Handler

```csharp
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Todos.GetOverdue;

internal sealed class GetOverdueTodosQueryHandler(
    ITodoItemStore todoItemStore,
    ICurrentTenantContext tenantContext,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetOverdueTodosQuery, List<TodoResponse>>
{
    public async Task<Result<List<TodoResponse>>> HandleAsync(
        GetOverdueTodosQuery query,
        CancellationToken cancellationToken) =>
        await todoItemStore.ListOverdueAsync(
            tenantContext.CurrentTenantId,
            tenantContext.CurrentUserId,
            dateTimeProvider.UtcNow,
            cancellationToken);
}
```

Single-item queries return `Result.Failure<TodoResponse>(TodoItemErrors.NotFound(id))` when the store returns null — including when the item belongs to another tenant.

## Caching (hot reads only)

Keys come from `{Feature}CacheKeys` and **must include the tenant**:

```csharp
TodoResponse? todo = await cache.GetOrCreateAsync(
    TodoCacheKeys.ById(tenantId, userId, query.TodoItemId),
    async cancellation => await todoItemStore.GetResponseAsync(tenantId, userId, query.TodoItemId, cancellation),
    cancellationToken: cancellationToken);
```

Every command that mutates the cached data removes the same key. If you can't enumerate the affected keys, don't cache.
