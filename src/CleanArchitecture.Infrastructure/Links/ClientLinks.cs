using CleanArchitecture.Application.Abstractions.Links;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Links;

internal sealed class ClientLinks(IOptions<ClientAppOptions> options) : IClientLinks
{
    public string AcceptInvitation(string token) => Build("accept-invitation", token);

    public string ResetPassword(string token) => Build("reset-password", token);

    public string ConfirmEmailChange(string token) => Build("confirm-email", token);

    private string Build(string path, string token) =>
        $"{options.Value.BaseUrl.TrimEnd('/')}/{path}?token={Uri.EscapeDataString(token)}";
}
