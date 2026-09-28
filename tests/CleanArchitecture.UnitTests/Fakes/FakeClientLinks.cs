using CleanArchitecture.Application.Abstractions.Links;

namespace CleanArchitecture.UnitTests.Fakes;

public sealed class FakeClientLinks : IClientLinks
{
    public string AcceptInvitation(string token) => $"https://app.test/accept-invitation?token={token}";

    public string ResetPassword(string token) => $"https://app.test/reset-password?token={token}";

    public string ConfirmEmailChange(string token) => $"https://app.test/confirm-email?token={token}";
}
