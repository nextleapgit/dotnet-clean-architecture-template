using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Application.Todos.Create;

internal sealed class CreateTodoCommandHandler(
    IUnitOfWork unitOfWork,
    ITodoItemStore todoItemStore,
    ICurrentTenantContext tenantContext,
    IUserStore userStore,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateTodoCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateTodoCommand command, CancellationToken cancellationToken)
    {
        TenantId tenantId = tenantContext.CurrentTenantId;
        Guid userId = tenantContext.CurrentUserId;

        if (!await userStore.ExistsAsync(tenantId, userId, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.NotFound(userId));
        }

        var todoItem = TodoItem.Create(
            tenantId,
            userId,
            command.Description,
            command.DueDate,
            command.Labels,
            command.Priority,
            dateTimeProvider.UtcNow);

        todoItemStore.Add(todoItem);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return todoItem.Id;
    }
}
