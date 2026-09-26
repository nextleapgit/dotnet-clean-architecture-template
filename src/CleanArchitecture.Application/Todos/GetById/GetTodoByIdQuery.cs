using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Todos.GetById;

public sealed record GetTodoByIdQuery(Guid TodoItemId) : IQuery<TodoResponse>;
