using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.Login;

public sealed record LoginUserCommand(string Email, string Password) : ICommand<AccessTokensResponse>;
