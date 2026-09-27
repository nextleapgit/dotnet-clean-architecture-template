namespace CleanArchitecture.Domain.Users;

/// <summary>Persisted as its number, so never renumber a value.</summary>
public enum UserTokenPurpose
{
    Invitation = 0,
    PasswordReset = 1,
    /// <summary>
    /// Reserved for confirming a new email address (the address travels in <see cref="UserToken.Payload"/>).
    /// Not implemented yet: no use case issues or redeems it.
    /// </summary>
    EmailChange = 2
}
