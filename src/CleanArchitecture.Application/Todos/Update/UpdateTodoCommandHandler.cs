using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.SharedKernel;
using Microsoft.Extensions.Caching.Hybrid;

namespace CleanArchitecture.Application.Todos.Update;

internal sealed class UpdateTodoCommandHandler(
    IUnitOfWork unitOfWork,
    ITodoItemStore todoItemStore,
    ICurrentTenantContext tenantContext,
    HybridCache cache)
    : ICommandHandler<UpdateTodoCommand>
{
    public async Task<Result> HandleAsync(UpdateTodoCommand command, CancellationToken cancellationToken)
    {
        TodoItem? todoItem = await todoItemStore.FindAsync(
            tenantContext.CurrentTenantId,
            tenantContext.CurrentUserId,
            command.TodoItemId,
            cancellationToken);

        if (todoItem is null)
        {
            return Result.Failure(TodoItemErrors.NotFound(command.TodoItemId));
        }

        todoItem.UpdateDescription(command.Description);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(TodoCacheKeys.ById(todoItem.TenantId, todoItem.UserId, todoItem.Id), cancellationToken);

        return Result.Success();
    }
}
