using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Todos;

public sealed record TodoItemUpdatedDomainEvent(Guid TodoItemId) : IDomainEvent;
