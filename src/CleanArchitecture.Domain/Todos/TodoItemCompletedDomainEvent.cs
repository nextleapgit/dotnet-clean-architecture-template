using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Todos;

public sealed record TodoItemCompletedDomainEvent(Guid TodoItemId) : IDomainEvent;
