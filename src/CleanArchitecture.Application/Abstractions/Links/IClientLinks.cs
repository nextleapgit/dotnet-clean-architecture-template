namespace CleanArchitecture.Application.Abstractions.Links;

/// <summary>Links into the client application, sent by email. The token is the only secret in them.</summary>
public interface IClientLinks
{
    string AcceptInvitation(string token);

    string ResetPassword(string token);
}
