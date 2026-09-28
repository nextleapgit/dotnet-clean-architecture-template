namespace CleanArchitecture.Domain.Users;

/// <summary>Persisted as its number, so never renumber a value.</summary>
public enum UserTokenPurpose
{
    Invitation = 0,
    PasswordReset = 1,
    /// <summary>Confirms a new email address; the address travels in <see cref="UserToken.Payload"/>.</summary>
    EmailChange = 2
}
