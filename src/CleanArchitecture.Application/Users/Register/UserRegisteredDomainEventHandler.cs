using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CleanArchitecture.Application.Users.Register;

internal sealed class UserRegisteredDomainEventHandler(ILogger<UserRegisteredDomainEventHandler> logger)
    : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public Task Handle(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("User {UserId} registered", domainEvent.UserId);

        return Task.CompletedTask;
    }
}
