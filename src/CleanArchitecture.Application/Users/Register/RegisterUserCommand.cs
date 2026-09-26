using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.Register;

public sealed record RegisterUserCommand(string Email, string FirstName, string LastName, string Password)
    : ICommand<Guid>;
