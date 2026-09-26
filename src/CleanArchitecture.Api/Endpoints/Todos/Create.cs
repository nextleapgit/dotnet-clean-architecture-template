using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.Application.Todos.Create;
using CleanArchitecture.Domain.Todos;

namespace CleanArchitecture.Api.Endpoints.Todos;

internal sealed class Create : IEndpoint
{
    public sealed class Request
    {
        public string Description { get; set; }
        public DateTime? DueDate { get; set; }
        public List<string> Labels { get; set; } = [];
        public int Priority { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("todos", async (
            Request request,
            ICommandDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateTodoCommand(
                request.Description,
                request.DueDate,
                request.Labels,
                (Priority)request.Priority);

            Result<Guid> result = await dispatcher.DispatchAsync<CreateTodoCommand, Guid>(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .HasPermission(Permissions.TodosWrite);
    }
}
