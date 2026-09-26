using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Todos.Complete;

public sealed record CompleteTodoCommand(Guid TodoItemId) : ICommand;
