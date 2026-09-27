namespace CleanArchitecture.Domain.Users;

/// <summary>
/// What a user may do. Persisted as its number, so never renumber a value.
/// </summary>
public enum Role
{
    /// <summary>Works with their own data only.</summary>
    Member = 0,

    /// <summary>Manages the users of their own tenant.</summary>
    Manager = 1,

    /// <summary>Platform operator: manages every tenant. Created only by the start-up bootstrap.</summary>
    Admin = 2
}
