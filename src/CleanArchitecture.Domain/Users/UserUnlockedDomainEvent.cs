using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Users;

public sealed record UserUnlockedDomainEvent(Guid UserId) : IDomainEvent;
