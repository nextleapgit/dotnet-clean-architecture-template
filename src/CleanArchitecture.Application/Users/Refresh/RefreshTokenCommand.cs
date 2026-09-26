using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<AccessTokensResponse>;
