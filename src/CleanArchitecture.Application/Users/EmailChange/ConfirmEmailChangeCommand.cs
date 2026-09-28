using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.EmailChange;

/// <summary>
/// Opening the link sent to the new address proves control of that mailbox and changes the email.
/// Anonymous: the link may be opened on a device that is not signed in.
/// </summary>
public sealed record ConfirmEmailChangeCommand(string Token) : ICommand;
