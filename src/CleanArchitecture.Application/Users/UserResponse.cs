namespace CleanArchitecture.Application.Users;

public sealed record UserResponse
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string Email { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }
}
