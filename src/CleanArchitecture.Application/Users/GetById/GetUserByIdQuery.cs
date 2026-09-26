using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.GetById;

public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserResponse>;
