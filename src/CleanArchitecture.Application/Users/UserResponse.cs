using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Application.Users;

public sealed record UserResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string Email { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    public Role Role { get; init; }

    public bool IsActive { get; init; }

    /// <summary>True until the user accepts the invitation and chooses a password.</summary>
    public bool InvitationPending { get; init; }

    /// <summary>Set while (or after) the account was locked by failed sign-ins.</summary>
    public DateTime? LockoutEndUtc { get; init; }
}
