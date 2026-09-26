using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.SharedKernel;
using Microsoft.Extensions.Caching.Hybrid;

namespace CleanArchitecture.Application.Todos.GetById;

internal sealed class GetTodoByIdQueryHandler(
    ITodoItemStore todoItemStore,
    ICurrentTenantContext tenantContext,
    HybridCache cache)
    : IQueryHandler<GetTodoByIdQuery, TodoResponse>
{
    public async Task<Result<TodoResponse>> HandleAsync(GetTodoByIdQuery query, CancellationToken cancellationToken)
    {
        TenantId tenantId = tenantContext.CurrentTenantId;
        Guid userId = tenantContext.CurrentUserId;

        TodoResponse? todo = await cache.GetOrCreateAsync(
            TodoCacheKeys.ById(tenantId, userId, query.TodoItemId),
            async cancellation => await todoItemStore.GetResponseAsync(tenantId, userId, query.TodoItemId, cancellation),
            cancellationToken: cancellationToken);

        return todo is null
            ? Result.Failure<TodoResponse>(TodoItemErrors.NotFound(query.TodoItemId))
            : todo;
    }
}
