using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Todos;

public sealed record TodoItemCreatedDomainEvent(Guid TodoItemId) : IDomainEvent;
