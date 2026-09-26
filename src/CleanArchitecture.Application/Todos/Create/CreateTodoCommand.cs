using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Domain.Todos;

namespace CleanArchitecture.Application.Todos.Create;

public sealed record CreateTodoCommand(
    string Description,
    DateTime? DueDate,
    List<string> Labels,
    Priority Priority) : ICommand<Guid>;
