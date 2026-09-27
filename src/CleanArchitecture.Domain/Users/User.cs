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
    public Role Role { get; private set; }
    public bool IsActive { get; private set; }

    public static User Create(
        TenantId tenantId,
        string email,
        string firstName,
        string lastName,
        string passwordHash,
        Role role)
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Email = NormalizeEmail(email),
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true
        };

        user.Raise(new UserCreatedDomainEvent(user.Id));

        return user;
    }

    public void UpdateName(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;

        Raise(new UserUpdatedDomainEvent(Id));
    }

    /// <summary>The Admin role is never assigned here: admins come only from the start-up bootstrap.</summary>
    public Result ChangeRole(Role role)
    {
        if (role == Role.Admin)
        {
            return Result.Failure(UserErrors.AdminRoleNotAssignable);
        }

        if (Role == role)
        {
            return Result.Success();
        }

        Role previousRole = Role;
        Role = role;

        Raise(new UserRoleChangedDomainEvent(Id, previousRole, role));

        return Result.Success();
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;

        Raise(new UserDeactivatedDomainEvent(Id));
    }

    public void Activate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;

        Raise(new UserActivatedDomainEvent(Id));
    }

    // Emails are stored lower-cased so uniqueness and lookups are case-insensitive.
#pragma warning disable CA1308 // Lower case is the conventional canonical form for email addresses.
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
#pragma warning restore CA1308
}
