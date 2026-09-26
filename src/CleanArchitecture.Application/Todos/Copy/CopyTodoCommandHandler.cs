using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Todos.Copy;

internal sealed class CopyTodoCommandHandler(
    IUnitOfWork unitOfWork,
    ITodoItemStore todoItemStore,
    ICurrentTenantContext tenantContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CopyTodoCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CopyTodoCommand command, CancellationToken cancellationToken)
    {
        TodoItem? existingTodo = await todoItemStore.FindAsync(
            tenantContext.CurrentTenantId,
            tenantContext.CurrentUserId,
            command.TodoItemId,
            cancellationToken);

        if (existingTodo is null)
        {
            return Result.Failure<Guid>(TodoItemErrors.NotFound(command.TodoItemId));
        }

        TodoItem copiedTodoItem = existingTodo.Copy(dateTimeProvider.UtcNow);

        todoItemStore.Add(copiedTodoItem);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return copiedTodoItem.Id;
    }
}
