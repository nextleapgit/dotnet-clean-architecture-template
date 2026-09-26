using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.Application.Todos.Update;

namespace CleanArchitecture.Api.Endpoints.Todos;

internal sealed class Update : IEndpoint
{
    public sealed record Request(string Description);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("todos/{id:guid}", async (
            Guid id,
            Request request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result result = await dispatcher.DispatchAsync(
                new UpdateTodoCommand(id, request.Description),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .HasPermission(Permissions.TodosWrite);
    }
}
