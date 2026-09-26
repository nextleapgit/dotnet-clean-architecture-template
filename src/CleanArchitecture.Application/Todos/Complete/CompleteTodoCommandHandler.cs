using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.SharedKernel;
using Microsoft.Extensions.Caching.Hybrid;

namespace CleanArchitecture.Application.Todos.Complete;

internal sealed class CompleteTodoCommandHandler(
    IUnitOfWork unitOfWork,
    ITodoItemStore todoItemStore,
    ICurrentTenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
    HybridCache cache)
    : ICommandHandler<CompleteTodoCommand>
{
    public async Task<Result> HandleAsync(CompleteTodoCommand command, CancellationToken cancellationToken)
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

        Result result = todoItem.Complete(dateTimeProvider.UtcNow);

        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(TodoCacheKeys.ById(todoItem.TenantId, todoItem.UserId, todoItem.Id), cancellationToken);

        return Result.Success();
    }
}
