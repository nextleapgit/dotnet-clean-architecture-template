using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.Logout;

public sealed record LogoutUserCommand(string RefreshToken) : ICommand;
