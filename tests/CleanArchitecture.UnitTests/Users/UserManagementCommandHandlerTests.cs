using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.Activate;
using CleanArchitecture.Application.Users.ChangeRole;
using CleanArchitecture.Application.Users.Deactivate;
using CleanArchitecture.Application.Users.Update;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Users;

public sealed class UserManagementCommandHandlerTests
{
    private readonly UserAdministrationFixture _fixture = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private ChangeUserRoleCommandHandler ChangeRoleHandler(User actor) =>
        new(_fixture.UnitOfWork, _fixture.UserManagementFor(actor), _fixture.AuditLog);

    private DeactivateUserCommandHandler DeactivateHandler(User actor) =>
        new(_fixture.UnitOfWork, _fixture.UserManagementFor(actor), _fixture.RefreshTokens, TestData.Clock(), _fixture.AuditLog);

    private ActivateUserCommandHandler ActivateHandler(User actor) =>
        new(_fixture.UnitOfWork, _fixture.UserManagementFor(actor), _fixture.AuditLog);

    private UpdateUserCommandHandler UpdateHandler(User actor) =>
        new(_fixture.UnitOfWork, _fixture.UserManagementFor(actor));

    [Fact]
    public async Task ChangeRole_Should_PromoteAndAudit_WhenManagerTargetsOwnTenantUser()
    {
        User member = _fixture.AddUser(_fixture.Tenant);

        Result result = await ChangeRoleHandler(_fixture.Manager)
            .HandleAsync(new ChangeUserRoleCommand(null, member.Id, Role.Manager), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        member.Role.ShouldBe(Role.Manager);
        AuditRecord audit = _fixture.AuditLog.Records.ShouldHaveSingleItem();
        audit.Action.ShouldBe(UserAuditActions.RoleChanged);
        audit.EntityId.ShouldBe(member.Id.ToString());
        _fixture.UnitOfWork.SaveChangesCount.ShouldBe(1);
    }

    [Fact]
    public async Task ChangeRole_Should_NotAudit_WhenRoleIsUnchanged()
    {
        User member = _fixture.AddUser(_fixture.Tenant);

        Result result = await ChangeRoleHandler(_fixture.Manager)
            .HandleAsync(new ChangeUserRoleCommand(null, member.Id, Role.Member), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _fixture.AuditLog.Records.ShouldBeEmpty();
        _fixture.UnitOfWork.SaveChangesCount.ShouldBe(0);
    }

    [Fact]
    public async Task ChangeRole_Should_Fail_WhenCallerTargetsThemselves()
    {
        Result result = await ChangeRoleHandler(_fixture.Manager)
            .HandleAsync(new ChangeUserRoleCommand(null, _fixture.Manager.Id, Role.Member), CancellationToken);

        result.Error.ShouldBe(UserErrors.CannotChangeOwnAccess);
        _fixture.Manager.Role.ShouldBe(Role.Manager);
    }

    [Fact]
    public async Task ChangeRole_Should_ReturnNotFound_WhenUserBelongsToAnotherTenant()
    {
        User foreign = _fixture.AddUser(_fixture.OtherTenant);

        Result result = await ChangeRoleHandler(_fixture.Manager)
            .HandleAsync(new ChangeUserRoleCommand(null, foreign.Id, Role.Manager), CancellationToken);

        result.Error.ShouldBe(UserErrors.NotFound(foreign.Id));
        foreign.Role.ShouldBe(Role.Member);
    }

    [Fact]
    public async Task ChangeRole_Should_Succeed_WhenAdminTargetsAnotherTenant()
    {
        User foreign = _fixture.AddUser(_fixture.OtherTenant);

        Result result = await ChangeRoleHandler(_fixture.Admin)
            .HandleAsync(new ChangeUserRoleCommand(_fixture.OtherTenant.Id.Value, foreign.Id, Role.Manager), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        foreign.Role.ShouldBe(Role.Manager);
    }

    [Fact]
    public async Task ChangeRole_Should_ReturnNotFound_WhenManagerNamesAnotherTenant()
    {
        User foreign = _fixture.AddUser(_fixture.OtherTenant);
        Guid other = _fixture.OtherTenant.Id.Value;

        Result result = await ChangeRoleHandler(_fixture.Manager)
            .HandleAsync(new ChangeUserRoleCommand(other, foreign.Id, Role.Manager), CancellationToken);

        result.Error.ShouldBe(TenantErrors.NotFound(other));
    }

    [Fact]
    public async Task Management_Should_RefuseAdminAccounts()
    {
        User otherAdmin = _fixture.AddUser(_fixture.Platform, Role.Admin);

        Result result = await DeactivateHandler(_fixture.Admin)
            .HandleAsync(new DeactivateUserCommand(null, otherAdmin.Id), CancellationToken);

        result.Error.ShouldBe(UserErrors.AdminNotManageable);
        otherAdmin.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Deactivate_Should_DeactivateRevokeSessionsAndAudit()
    {
        User member = _fixture.AddUser(_fixture.Tenant);
        var session = RefreshToken.Issue(member.Id, "hash", TestData.UtcNow, TimeSpan.FromDays(7));
        _fixture.RefreshTokens.Add(session);

        Result result = await DeactivateHandler(_fixture.Manager)
            .HandleAsync(new DeactivateUserCommand(null, member.Id), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        member.IsActive.ShouldBeFalse();
        session.IsRevoked.ShouldBeTrue();
        AuditRecord audit = _fixture.AuditLog.Records.ShouldHaveSingleItem();
        audit.Action.ShouldBe(UserAuditActions.Deactivated);
        audit.Metadata!["revokedSessions"].ShouldBe("1");
    }

    [Fact]
    public async Task Deactivate_Should_Fail_WhenCallerTargetsThemselves()
    {
        Result result = await DeactivateHandler(_fixture.Manager)
            .HandleAsync(new DeactivateUserCommand(null, _fixture.Manager.Id), CancellationToken);

        result.Error.ShouldBe(UserErrors.CannotChangeOwnAccess);
        _fixture.Manager.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Activate_Should_ReactivateAndAudit_WhenUserIsInactive()
    {
        User member = _fixture.AddUser(_fixture.Tenant);
        member.Deactivate();

        Result result = await ActivateHandler(_fixture.Manager)
            .HandleAsync(new ActivateUserCommand(null, member.Id), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        member.IsActive.ShouldBeTrue();
        _fixture.AuditLog.Records.ShouldHaveSingleItem().Action.ShouldBe(UserAuditActions.Activated);
    }

    [Fact]
    public async Task Update_Should_RenameUser_IncludingTheCallerThemselves()
    {
        Result result = await UpdateHandler(_fixture.Manager)
            .HandleAsync(new UpdateUserCommand(null, _fixture.Manager.Id, "Renamed", "Manager"), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _fixture.Manager.FirstName.ShouldBe("Renamed");
        _fixture.UnitOfWork.SaveChangesCount.ShouldBe(1);
    }

    [Fact]
    public async Task Update_Should_ReturnNotFound_WhenUserBelongsToAnotherTenant()
    {
        User foreign = _fixture.AddUser(_fixture.OtherTenant);

        Result result = await UpdateHandler(_fixture.Manager)
            .HandleAsync(new UpdateUserCommand(null, foreign.Id, "Hacked", "Name"), CancellationToken);

        result.Error.ShouldBe(UserErrors.NotFound(foreign.Id));
        foreign.FirstName.ShouldBe("Test");
    }
}
