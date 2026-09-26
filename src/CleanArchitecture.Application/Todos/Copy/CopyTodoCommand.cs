using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Todos.Copy;

public sealed record CopyTodoCommand(Guid TodoItemId) : ICommand<Guid>;
