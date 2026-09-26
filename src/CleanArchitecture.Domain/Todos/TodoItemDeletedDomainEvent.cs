using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Todos;

public sealed record TodoItemDeletedDomainEvent(Guid TodoItemId) : IDomainEvent;
