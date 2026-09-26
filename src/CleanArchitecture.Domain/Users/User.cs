using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Users;

public sealed class User : Entity
{
    private User()
    {
    }

    public const int EmailMaxLength = 254;
    public const int NameMaxLength = 100;

    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string PasswordHash { get; private set; }

    public static User Create(TenantId tenantId, string email, string firstName, string lastName, string passwordHash)
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Email = NormalizeEmail(email),
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = passwordHash
        };

        user.Raise(new UserRegisteredDomainEvent(user.Id));

        return user;
    }

    // Emails are stored lower-cased so uniqueness and lookups are case-insensitive.
#pragma warning disable CA1308 // Lower case is the conventional canonical form for email addresses.
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
#pragma warning restore CA1308
}
