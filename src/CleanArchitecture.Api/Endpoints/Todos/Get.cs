using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.Application.Todos;
using CleanArchitecture.Application.Todos.Get;

namespace CleanArchitecture.Api.Endpoints.Todos;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("todos", async (
            int? page,
            int? pageSize,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<List<TodoResponse>> result = await dispatcher.DispatchAsync<GetTodosQuery, List<TodoResponse>>(
                new GetTodosQuery(page ?? 1, pageSize ?? 50),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .HasPermission(Permissions.TodosRead);
    }
}
