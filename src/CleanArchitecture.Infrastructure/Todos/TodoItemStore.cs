using CleanArchitecture.Application.Todos;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Todos;

internal sealed class TodoItemStore(ApplicationDbContext dbContext) : ITodoItemStore
{
    public Task<TodoItem?> FindAsync(
        TenantId tenantId,
        Guid userId,
        Guid todoItemId,
        CancellationToken cancellationToken) =>
        Owned(tenantId, userId).SingleOrDefaultAsync(t => t.Id == todoItemId, cancellationToken);

    public Task<TodoResponse?> GetResponseAsync(
        TenantId tenantId,
        Guid userId,
        Guid todoItemId,
        CancellationToken cancellationToken) =>
        ToResponse(Owned(tenantId, userId).Where(t => t.Id == todoItemId)).SingleOrDefaultAsync(cancellationToken);

    public Task<List<TodoResponse>> ListResponsesAsync(
        TenantId tenantId,
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        ToResponse(Owned(tenantId, userId).OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(cancellationToken);

    public void Add(TodoItem todoItem) => dbContext.TodoItems.Add(todoItem);

    public void Remove(TodoItem todoItem) => dbContext.TodoItems.Remove(todoItem);

    // The single place that scopes todo items; every read goes through it.
    private IQueryable<TodoItem> Owned(TenantId tenantId, Guid userId) =>
        dbContext.TodoItems.Where(t => t.TenantId == tenantId && t.UserId == userId);

    private static IQueryable<TodoResponse> ToResponse(IQueryable<TodoItem> todoItems) =>
        todoItems.Select(todoItem => new TodoResponse
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
        });
}
