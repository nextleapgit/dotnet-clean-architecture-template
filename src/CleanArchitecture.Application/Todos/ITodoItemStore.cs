using CleanArchitecture.Domain.Todos;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Todos;

/// <summary>Every read is scoped to a tenant and to the owning user.</summary>
public interface ITodoItemStore
{
    Task<TodoItem?> FindAsync(TenantId tenantId, Guid userId, Guid todoItemId, CancellationToken cancellationToken);

    Task<TodoResponse?> GetResponseAsync(
        TenantId tenantId,
        Guid userId,
        Guid todoItemId,
        CancellationToken cancellationToken);

    Task<List<TodoResponse>> ListResponsesAsync(TenantId tenantId, Guid userId, int page, int pageSize, CancellationToken cancellationToken);

    void Add(TodoItem todoItem);

    void Remove(TodoItem todoItem);
}
