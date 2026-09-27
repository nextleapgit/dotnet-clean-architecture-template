using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Users;

public sealed record UserActivatedDomainEvent(Guid UserId) : IDomainEvent;
