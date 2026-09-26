# Command Slice Templates

Files go in `src/CleanArchitecture.Application/{Feature}/{UseCase}/`.

## Command

```csharp
using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Todos.Archive;

public sealed record ArchiveTodoCommand(Guid TodoItemId) : ICommand;
```

Returning a value: `public sealed record CreateTodoCommand(string Description, DateTime? DueDate, List<string> Labels, Priority Priority) : ICommand<Guid>;`

Never put the current user or tenant id in a command — handlers read them from `ICurrentTenantContext`.

## Validator

```csharp
using FluentValidation;

namespace CleanArchitecture.Application.Todos.Archive;

internal sealed class ArchiveTodoCommandValidator : AbstractValidator<ArchiveTodoCommand>
{
    public ArchiveTodoCommandValidator()
    {
        RuleFor(c => c.TodoItemId).NotEmpty();
    }
}
```

Validators may take dependencies through their constructor (e.g. `IDateTimeProvider` for "not in the past" rules) and use entity constants for lengths (`TodoItem.DescriptionMaxLength`).

## Handler

```csharp
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.SharedKernel;
using Microsoft.Extensions.Caching.Hybrid;

namespace CleanArchitecture.Application.Todos.Archive;

internal sealed class ArchiveTodoCommandHandler(
    IUnitOfWork unitOfWork,
    ITodoItemStore todoItemStore,
    ICurrentTenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
    HybridCache cache)
    : ICommandHandler<ArchiveTodoCommand>
{
    public async Task<Result> HandleAsync(ArchiveTodoCommand command, CancellationToken cancellationToken)
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

        Result result = todoItem.Archive(dateTimeProvider.UtcNow);

        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(TodoCacheKeys.ById(todoItem.TenantId, todoItem.UserId, todoItem.Id), cancellationToken);

        return Result.Success();
    }
}
```

## Entity behavior

```csharp
public Result Archive(DateTime archivedAt)
{
    if (IsArchived)
    {
        return Result.Failure(TodoItemErrors.AlreadyArchived(Id));
    }

    IsArchived = true;
    ArchivedAt = archivedAt;

    Raise(new TodoItemArchivedDomainEvent(Id));

    return Result.Success();
}
```

Error factory on `{Entity}Errors`:

```csharp
public static Error AlreadyArchived(Guid todoItemId) => Error.Problem(
    "TodoItems.AlreadyArchived",
    $"The todo item with Id = '{todoItemId}' is already archived.");
```

Error type → HTTP status: `Problem`/`Validation` → 400, `Unauthorized` → 401, `Forbidden` → 403, `NotFound` → 404, `Conflict` → 409, `Failure` → 500 (unexpected only; the body never reveals its details).

## Auditing a sensitive command

```csharp
auditLog.Record(new AuditRecord(ProjectAuditActions.MemberRemoved, nameof(Project), project.Id.ToString(), AuditSeverity.Warning)
{
    OldValues = new { memberId },
    Metadata = new Dictionary<string, string> { ["reason"] = command.Reason }
});

await unitOfWork.SaveChangesAsync(cancellationToken); // audit entry commits with the change
```

Tenant, user, correlation id, IP address, user agent, and timestamp are filled in by the audit log. Never record passwords, tokens, or secrets.

## Sending an email with the change

```csharp
Result<EmailMessage> email = EmailMessage.Create(Guid.CreateVersion7(), user.Email, "Subject", text, html);
if (email.IsFailure) { return Result.Failure(email.Error); }

await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
// ... domain write ...
await unitOfWork.SaveChangesAsync(cancellationToken);
await emailOutbox.EnqueueAsync(email.Value, expiresAtUtc, cancellationToken); // bounded by any link/token validity
await transaction.CommitAsync(cancellationToken);
```

See `RegisterUserCommandHandler` for the live example.
