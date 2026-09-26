using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Todos.Get;

public sealed record GetTodosQuery : IQuery<List<TodoResponse>>;
