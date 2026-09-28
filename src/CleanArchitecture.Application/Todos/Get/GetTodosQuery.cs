using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Todos.Get;

public sealed record GetTodosQuery(int Page = 1, int PageSize = 50) : IQuery<List<TodoResponse>>;
