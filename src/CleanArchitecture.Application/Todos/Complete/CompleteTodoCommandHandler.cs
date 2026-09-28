using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Todos.Complete;

internal sealed class CompleteTodoCommandHandler(
    IUnitOfWork unitOfWork,
    ITodoItemStore todoItemStore,
    ICurrentTenantContext tenantContext,
    IDateTimeProvider dateTimeProvider)
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


        return Result.Success();
    }
}
