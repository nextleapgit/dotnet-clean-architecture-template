using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.GetCurrent;

public sealed record GetCurrentUserQuery : IQuery<UserResponse>;
