using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.Passwords;

/// <summary>The signed-in user changes their own password; every other session is signed out.</summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand;
