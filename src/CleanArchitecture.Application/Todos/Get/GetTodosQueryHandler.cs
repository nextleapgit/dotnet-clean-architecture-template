using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Todos.Get;

internal sealed class GetTodosQueryHandler(ITodoItemStore todoItemStore, ICurrentTenantContext tenantContext)
    : IQueryHandler<GetTodosQuery, List<TodoResponse>>
{
    public async Task<Result<List<TodoResponse>>> HandleAsync(GetTodosQuery query, CancellationToken cancellationToken) =>
        await todoItemStore.ListResponsesAsync(
            tenantContext.CurrentTenantId,
            tenantContext.CurrentUserId,
            query.Page,
            query.PageSize,
            cancellationToken);
}
