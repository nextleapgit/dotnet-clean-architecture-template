using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Todos.Delete;

public sealed record DeleteTodoCommand(Guid TodoItemId) : ICommand;
