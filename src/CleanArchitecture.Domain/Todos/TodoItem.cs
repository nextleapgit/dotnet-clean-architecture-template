using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Todos;

public sealed class TodoItem : Entity
{
    public const int DescriptionMaxLength = 500;
    public const int MaxLabels = 20;
    public const int LabelMaxLength = 100;

    private TodoItem()
    {
    }

    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Description { get; private set; }
    public DateTime? DueDate { get; private set; }
    public List<string> Labels { get; private set; } = [];
    public bool IsCompleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Priority Priority { get; private set; }

    public static TodoItem Create(
        TenantId tenantId,
        Guid userId,
        string description,
        DateTime? dueDate,
        IEnumerable<string> labels,
        Priority priority,
        DateTime createdAt)
    {
        var todoItem = new TodoItem
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            UserId = userId,
            Description = description,
            DueDate = dueDate,
            Labels = [.. labels],
            Priority = priority,
            IsCompleted = false,
            CreatedAt = createdAt
        };

        todoItem.Raise(new TodoItemCreatedDomainEvent(todoItem.Id));

        return todoItem;
    }

    public TodoItem Copy(DateTime createdAt) =>
        Create(TenantId, UserId, Description, DueDate, Labels, Priority, createdAt);

    public Result Complete(DateTime completedAt)
    {
        if (IsCompleted)
        {
            return Result.Failure(TodoItemErrors.AlreadyCompleted(Id));
        }

        IsCompleted = true;
        CompletedAt = completedAt;

        Raise(new TodoItemCompletedDomainEvent(Id));

        return Result.Success();
    }

    public void UpdateDescription(string description)
    {
        Description = description;

        Raise(new TodoItemUpdatedDomainEvent(Id));
    }

    public void Delete()
    {
        Raise(new TodoItemDeletedDomainEvent(Id));
    }
}
