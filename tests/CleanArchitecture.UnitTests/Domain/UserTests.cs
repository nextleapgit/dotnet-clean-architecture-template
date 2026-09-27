using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Domain;

public sealed class UserTests
{
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    [Fact]
    public void Create_Should_BeActiveWithTheGivenRole()
    {
        var user = User.Create(TenantId.New(), "a@example.com", "A", "B", "hash", Role.Manager);

        user.IsActive.ShouldBeTrue();
        user.Role.ShouldBe(Role.Manager);
        user.HasPassword.ShouldBeTrue();
        user.DomainEvents.ShouldContain(new UserCreatedDomainEvent(user.Id));
    }

    [Fact]
    public void Create_Should_HaveNoPassword_WhenInvited()
    {
        var user = User.Create(TenantId.New(), "a@example.com", "A", "B", passwordHash: null, Role.Member);

        user.HasPassword.ShouldBeFalse();
    }

    [Fact]
    public void ChangeRole_Should_RaiseEvent_OnlyWhenRoleChanges()
    {
        User user = TestData.NewUser();

        user.ChangeRole(Role.Member);
        user.ChangeRole(Role.Manager);

        user.Role.ShouldBe(Role.Manager);
        user.DomainEvents.ShouldHaveSingleItem().ShouldBe(new UserRoleChangedDomainEvent(user.Id, Role.Member, Role.Manager));
    }

    [Fact]
    public void RecordFailedLogin_Should_LockTheAccount_OnTheLastAllowedAttempt()
    {
        User user = TestData.NewUser();

        for (int attempt = 1; attempt < 5; attempt++)
        {
            user.RecordFailedLogin(TestData.UtcNow, 5, Lockout).ShouldBeFalse();
        }

        bool locked = user.RecordFailedLogin(TestData.UtcNow, 5, Lockout);

        locked.ShouldBeTrue();
        user.IsLockedOut(TestData.UtcNow).ShouldBeTrue();
        user.IsLockedOut(TestData.UtcNow.Add(Lockout)).ShouldBeFalse();
        user.FailedLoginAttempts.ShouldBe(0);
        user.DomainEvents.ShouldHaveSingleItem().ShouldBe(new UserLockedOutDomainEvent(user.Id, TestData.UtcNow.Add(Lockout)));
    }

    [Fact]
    public void RecordSuccessfulLogin_Should_ResetTheFailureCount()
    {
        User user = TestData.NewUser();
        user.RecordFailedLogin(TestData.UtcNow, 5, Lockout);

        user.RecordSuccessfulLogin();

        user.FailedLoginAttempts.ShouldBe(0);
    }

    [Fact]
    public void SetPassword_Should_SetTheHashAndLiftALockout()
    {
        User user = TestData.NewUser();
        user.RecordFailedLogin(TestData.UtcNow, 1, Lockout);

        user.SetPassword("new-hash");

        user.PasswordHash.ShouldBe("new-hash");
        user.IsLockedOut(TestData.UtcNow).ShouldBeFalse();
        user.DomainEvents.ShouldContain(new UserPasswordChangedDomainEvent(user.Id));
    }

    [Fact]
    public void Unlock_Should_RaiseEvent_OnlyWhenTheAccountWasLocked()
    {
        User user = TestData.NewUser();

        user.Unlock();
        user.DomainEvents.ShouldBeEmpty();

        user.RecordFailedLogin(TestData.UtcNow, 1, Lockout);
        user.ClearDomainEvents();
        user.Unlock();

        user.IsLockedOut(TestData.UtcNow).ShouldBeFalse();
        user.DomainEvents.ShouldHaveSingleItem().ShouldBe(new UserUnlockedDomainEvent(user.Id));
    }

    [Fact]
    public void UserToken_Should_BeUsableOnlyOnceAndBeforeExpiry()
    {
        var token = UserToken.Issue(Guid.NewGuid(), UserTokenPurpose.PasswordReset, "hash", TestData.UtcNow, TimeSpan.FromHours(1));

        token.IsUsable(TestData.UtcNow).ShouldBeTrue();
        token.IsUsable(TestData.UtcNow.AddHours(1)).ShouldBeFalse();

        token.Consume(TestData.UtcNow);

        token.IsUsable(TestData.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void DeactivateAndActivate_Should_ToggleAndRaiseEventsOnlyOnChange()
    {
        User user = TestData.NewUser();

        user.Deactivate();
        user.Deactivate();
        user.IsActive.ShouldBeFalse();

        user.Activate();
        user.Activate();
        user.IsActive.ShouldBeTrue();

        user.DomainEvents.ShouldBe(
        [
            new UserDeactivatedDomainEvent(user.Id),
            new UserActivatedDomainEvent(user.Id)
        ]);
    }

    [Fact]
    public void TenantDeactivateAndActivate_Should_ToggleAndRaiseEventsOnlyOnChange()
    {
        Tenant tenant = TestData.NewTenant();

        tenant.Deactivate();
        tenant.Deactivate();
        tenant.IsActive.ShouldBeFalse();

        tenant.Activate();
        tenant.IsActive.ShouldBeTrue();

        tenant.DomainEvents.ShouldBe(
        [
            new TenantDeactivatedDomainEvent(tenant.Id),
            new TenantActivatedDomainEvent(tenant.Id)
        ]);
    }

    [Fact]
    public void TenantCreatePlatform_Should_MarkThePlatformTenant()
    {
        Tenant.CreatePlatform("Platform", TestData.UtcNow).IsPlatform.ShouldBeTrue();
        Tenant.Create("Acme", TestData.UtcNow).IsPlatform.ShouldBeFalse();
    }
}
