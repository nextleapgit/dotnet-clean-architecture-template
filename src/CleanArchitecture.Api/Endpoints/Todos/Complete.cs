using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.Application.Todos.Complete;

namespace CleanArchitecture.Api.Endpoints.Todos;

internal sealed class Complete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("todos/{id:guid}/complete", async (
            Guid id,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result result = await dispatcher.DispatchAsync(new CompleteTodoCommand(id), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .HasPermission(Permissions.TodosWrite);
    }
}
