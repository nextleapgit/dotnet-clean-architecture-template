using CleanArchitecture.BuildingBlocks.Cqrs;

namespace CleanArchitecture.Application.Users.Passwords;

/// <summary>Always succeeds, so the answer does not reveal which emails have an account.</summary>
public sealed record ForgotPasswordCommand(string Email) : ICommand;
