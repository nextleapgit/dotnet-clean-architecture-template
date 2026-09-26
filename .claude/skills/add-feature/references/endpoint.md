# Endpoint Templates

One file per use case in `src/CleanArchitecture.Api/Endpoints/{Feature}/{UseCase}.cs`. Endpoints implement `IEndpoint` and are auto-discovered; `MapVersionedEndpoints` maps them all under `/api/v1`.

## Command with a body (POST → 200 + value)

```csharp
using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Todos.Create;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.SharedKernel;

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
            var command = new CreateTodoCommand(request.Description, request.DueDate, request.Labels, (Priority)request.Priority);

            Result<Guid> result = await dispatcher.DispatchAsync<CreateTodoCommand, Guid>(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .HasPermission(Permissions.TodosWrite);
    }
}
```

## Void command (PUT/DELETE → 204)

```csharp
app.MapPut("todos/{id:guid}/archive", async (
    Guid id,
    ICommandDispatcher dispatcher,
    CancellationToken cancellationToken) =>
{
    Result result = await dispatcher.DispatchAsync(new ArchiveTodoCommand(id), cancellationToken);

    return result.Match(Results.NoContent, CustomResults.Problem);
})
.WithTags(Tags.Todos)
.HasPermission(Permissions.TodosWrite);
```

## Query (GET → 200)

```csharp
app.MapGet("todos/overdue", async (
    IQueryDispatcher dispatcher,
    CancellationToken cancellationToken) =>
{
    Result<List<TodoResponse>> result = await dispatcher.DispatchAsync<GetOverdueTodosQuery, List<TodoResponse>>(
        new GetOverdueTodosQuery(),
        cancellationToken);

    return result.Match(Results.Ok, CustomResults.Problem);
})
.WithTags(Tags.Todos)
.HasPermission(Permissions.TodosRead);
```

## Rules

- Routes are relative to the version group: lowercase, plural, no leading slash, `:guid` constraints on typed parameters. Breaking changes get a new `/api/v2` group.
- Every route has `.HasPermission(Permissions.X)`. Add new codes (`resource:action`) to `Permissions` and to the default set in `PermissionProvider`. Only sign-in style routes are `.AllowAnonymous()` — an integration test enforces this list.
- No business logic, no store or DbContext access in endpoints (architecture test). The nested `Request` exists only for JSON bodies.
- Failures are translated only by `CustomResults.Problem`: RFC 9457 problem details with `code` (stable, localizable) and `correlationId`.
- Authentication endpoints also get `.RequireRateLimiting(RateLimitingPolicies.Authentication)`.
