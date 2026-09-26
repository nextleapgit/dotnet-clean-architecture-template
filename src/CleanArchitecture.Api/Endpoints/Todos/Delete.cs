using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.Application.Todos.Delete;

namespace CleanArchitecture.Api.Endpoints.Todos;

internal sealed class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("todos/{id:guid}", async (
            Guid id,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result result = await dispatcher.DispatchAsync(new DeleteTodoCommand(id), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .HasPermission(Permissions.TodosWrite);
    }
}
