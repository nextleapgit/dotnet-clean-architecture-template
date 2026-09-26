using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Todos.Update;

public sealed record UpdateTodoCommand(
    Guid TodoItemId,
    string Description) : ICommand;
