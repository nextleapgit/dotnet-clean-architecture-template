using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.Invitations;

public sealed record AcceptInvitationCommand(string Token, string Password) : ICommand;
