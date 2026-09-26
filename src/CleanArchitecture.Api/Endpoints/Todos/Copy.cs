using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.Application.Todos.Copy;

namespace CleanArchitecture.Api.Endpoints.Todos;

internal sealed class Copy : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("todos/{id:guid}/copy", async (
            Guid id,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await dispatcher.DispatchAsync<CopyTodoCommand, Guid>(
                new CopyTodoCommand(id),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .HasPermission(Permissions.TodosWrite);
    }
}
