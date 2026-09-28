using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Todos.GetById;

internal sealed class GetTodoByIdQueryHandler(
    ITodoItemStore todoItemStore,
    ICurrentTenantContext tenantContext)
    : IQueryHandler<GetTodoByIdQuery, TodoResponse>
{
    public async Task<Result<TodoResponse>> HandleAsync(GetTodoByIdQuery query, CancellationToken cancellationToken)
    {
        TenantId tenantId = tenantContext.CurrentTenantId;
        Guid userId = tenantContext.CurrentUserId;

        TodoResponse? todo = await todoItemStore.GetResponseAsync(tenantId, userId, query.TodoItemId, cancellationToken);

        return todo is null
            ? Result.Failure<TodoResponse>(TodoItemErrors.NotFound(query.TodoItemId))
            : todo;
    }
}
