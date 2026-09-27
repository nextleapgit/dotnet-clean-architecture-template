using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Domain;

public sealed class UserTests
{
    [Fact]
    public void Create_Should_BeActiveWithTheGivenRole()
    {
        var user = User.Create(TenantId.New(), "a@example.com", "A", "B", "hash", Role.Manager);

        user.IsActive.ShouldBeTrue();
        user.Role.ShouldBe(Role.Manager);
        user.DomainEvents.ShouldContain(new UserCreatedDomainEvent(user.Id));
    }

    [Fact]
    public void ChangeRole_Should_RaiseEvent_WhenRoleChanges()
    {
        User user = TestData.NewUser();

        Result result = user.ChangeRole(Role.Manager);

        result.IsSuccess.ShouldBeTrue();
        user.Role.ShouldBe(Role.Manager);
        user.DomainEvents.ShouldHaveSingleItem().ShouldBe(new UserRoleChangedDomainEvent(user.Id, Role.Member, Role.Manager));
    }

    [Fact]
    public void ChangeRole_Should_DoNothing_WhenRoleIsUnchanged()
    {
        User user = TestData.NewUser();

        user.ChangeRole(Role.Member).IsSuccess.ShouldBeTrue();

        user.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ChangeRole_Should_Fail_WhenRoleIsAdmin()
    {
        User user = TestData.NewUser();

        Result result = user.ChangeRole(Role.Admin);

        result.Error.ShouldBe(UserErrors.AdminRoleNotAssignable);
        user.Role.ShouldBe(Role.Member);
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
}
