using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.EmailChange;

/// <summary>
/// The signed-in user asks to change their email address. Nothing changes until the link sent to the
/// new address is opened (<see cref="ConfirmEmailChangeCommand"/>).
/// </summary>
public sealed record RequestEmailChangeCommand(string CurrentPassword, string NewEmail) : ICommand;
