using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.Passwords;

/// <summary>Sets a new password from an emailed reset link; every session is signed out.</summary>
public sealed record ResetPasswordCommand(string Token, string NewPassword) : ICommand;
