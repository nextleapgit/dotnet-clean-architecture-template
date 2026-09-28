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

    /// <summary>Null until the user accepts their invitation and chooses a password.</summary>
    public string? PasswordHash { get; private set; }

    public Role Role { get; private set; }
    public bool IsActive { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockoutEndUtc { get; private set; }

    public bool HasPassword => PasswordHash is not null;

    /// <param name="passwordHash">Null for an invited user, who sets the password when accepting the invitation.</param>
    public static User Create(
        TenantId tenantId,
        string email,
        string firstName,
        string lastName,
        string? passwordHash,
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

    /// <summary>
    /// The address is the sign-in name, so the use case proves control of the new mailbox and its
    /// uniqueness first; the entity only normalizes and records it.
    /// </summary>
    public void ChangeEmail(string email)
    {
        string normalized = NormalizeEmail(email);

        if (Email == normalized)
        {
            return;
        }

        Email = normalized;

        Raise(new UserEmailChangedDomainEvent(Id));
    }

    /// <summary>Who may assign which role is decided by the use case; the entity only records it.</summary>
    public void ChangeRole(Role role)
    {
        if (Role == role)
        {
            return;
        }

        Role previousRole = Role;
        Role = role;

        Raise(new UserRoleChangedDomainEvent(Id, previousRole, role));
    }

    /// <summary>Setting a password also clears any lockout: the user proved control of the account.</summary>
    public void SetPassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        FailedLoginAttempts = 0;
        LockoutEndUtc = null;

        Raise(new UserPasswordChangedDomainEvent(Id));
    }

    public bool IsLockedOut(DateTime utcNow) => LockoutEndUtc is { } lockoutEnd && lockoutEnd > utcNow;

    /// <returns>True when this failure locked the account.</returns>
    public bool RecordFailedLogin(DateTime utcNow, int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginAttempts++;

        if (FailedLoginAttempts < maxFailedAttempts)
        {
            return false;
        }

        FailedLoginAttempts = 0;
        LockoutEndUtc = utcNow.Add(lockoutDuration);

        Raise(new UserLockedOutDomainEvent(Id, LockoutEndUtc.Value));

        return true;
    }

    public void RecordSuccessfulLogin()
    {
        FailedLoginAttempts = 0;
        LockoutEndUtc = null;
    }

    public void Unlock()
    {
        bool wasLocked = LockoutEndUtc is not null;

        FailedLoginAttempts = 0;
        LockoutEndUtc = null;

        if (wasLocked)
        {
            Raise(new UserUnlockedDomainEvent(Id));
        }
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
