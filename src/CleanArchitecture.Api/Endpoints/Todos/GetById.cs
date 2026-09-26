using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.Application.Todos;
using CleanArchitecture.Application.Todos.GetById;

namespace CleanArchitecture.Api.Endpoints.Todos;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("todos/{id:guid}", async (
            Guid id,
            IQueryDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<TodoResponse> result = await dispatcher.DispatchAsync<GetTodoByIdQuery, TodoResponse>(
                new GetTodoByIdQuery(id),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .HasPermission(Permissions.TodosRead);
    }
}
