using CleanArchitecture.Application.Todos;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.UnitTests.Fakes;

public sealed class InMemoryTodoItemStore : ITodoItemStore
{
    public List<TodoItem> Items { get; } = [];

    public Task<TodoItem?> FindAsync(TenantId tenantId, Guid userId, Guid todoItemId, CancellationToken cancellationToken) =>
        Task.FromResult(Owned(tenantId, userId).SingleOrDefault(t => t.Id == todoItemId));

    public Task<TodoResponse?> GetResponseAsync(
        TenantId tenantId,
        Guid userId,
        Guid todoItemId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Owned(tenantId, userId).Where(t => t.Id == todoItemId).Select(ToResponse).SingleOrDefault());

    public Task<List<TodoResponse>> ListResponsesAsync(TenantId tenantId, Guid userId, int page, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult(Owned(tenantId, userId).OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(ToResponse).ToList());

    public void Add(TodoItem todoItem) => Items.Add(todoItem);

    public void Remove(TodoItem todoItem) => Items.Remove(todoItem);

    private IEnumerable<TodoItem> Owned(TenantId tenantId, Guid userId) =>
        Items.Where(t => t.TenantId == tenantId && t.UserId == userId);

    private static TodoResponse ToResponse(TodoItem todoItem) =>
        new()
        {
            Id = todoItem.Id,
            UserId = todoItem.UserId,
            Description = todoItem.Description,
            DueDate = todoItem.DueDate,
            Labels = todoItem.Labels,
            Priority = todoItem.Priority,
            IsCompleted = todoItem.IsCompleted,
            CreatedAt = todoItem.CreatedAt,
            CompletedAt = todoItem.CompletedAt
        };
}
