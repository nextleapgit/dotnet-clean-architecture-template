using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.Create;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.UnitTests.Users;

public sealed class CreateUserCommandHandlerTests
{
    private readonly UserAdministrationFixture _fixture = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private CreateUserCommandHandler HandlerFor(User actor) => new(
        _fixture.UnitOfWork,
        _fixture.TenantAccessFor(actor),
        _fixture.UserManagementFor(actor),
        _fixture.Users,
        _fixture.TokenIssuer(),
        _fixture.ClientLinks,
        _fixture.AuditLog,
        _fixture.EmailOutbox);

    private static CreateUserCommand Command(Guid? tenantId = null, string email = "new@example.com", Role role = Role.Member) =>
        new(tenantId, email, "New", "User", role);

    [Fact]
    public async Task Handle_Should_CreateInvitedUserAndEmailTheInvitationAtomically_WhenCallerIsManager()
    {
        // Act
        Result<Guid> result = await HandlerFor(_fixture.Manager).HandleAsync(Command(role: Role.Manager), CancellationToken);

        // Assert
        User user = _fixture.Users.Users.Single(u => u.Id == result.Value);
        user.TenantId.ShouldBe(_fixture.Tenant.Id);
        user.Role.ShouldBe(Role.Manager);
        user.HasPassword.ShouldBeFalse();

        UserToken invitation = _fixture.UserTokens.Tokens.ShouldHaveSingleItem();
        invitation.UserId.ShouldBe(user.Id);
        invitation.Purpose.ShouldBe(UserTokenPurpose.Invitation);
        invitation.TokenHash.ShouldBe("hash(refresh-1)"); // only the hash is stored

        (EmailMessage message, DateTime expiresAtUtc) = _fixture.EmailOutbox.Enqueued.ShouldHaveSingleItem();
        message.Recipient.ShouldBe("new@example.com");
        message.TextBody.ShouldContain("https://app.test/accept-invitation?token=refresh-1");
        expiresAtUtc.ShouldBe(invitation.ExpiresAtUtc);

        AuditRecord audit = _fixture.AuditLog.Records.ShouldHaveSingleItem();
        audit.Action.ShouldBe(UserAuditActions.Created);
        audit.EntityId.ShouldBe(user.Id.ToString());
        audit.TenantId.ShouldBe(_fixture.Tenant.Id);

        _fixture.UnitOfWork.SavesInsideTransaction.ShouldBe(1);
        _fixture.UnitOfWork.Committed.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_CreateUserInRequestedTenant_WhenCallerIsAdmin()
    {
        Result<Guid> result = await HandlerFor(_fixture.Admin)
            .HandleAsync(Command(_fixture.OtherTenant.Id.Value), CancellationToken);

        _fixture.Users.Users.Single(u => u.Id == result.Value).TenantId.ShouldBe(_fixture.OtherTenant.Id);
    }

    [Fact]
    public async Task Handle_Should_CreateAnotherAdmin_WhenAdminTargetsThePlatformTenant()
    {
        Result<Guid> result = await HandlerFor(_fixture.Admin)
            .HandleAsync(Command(_fixture.Platform.Id.Value, role: Role.Admin), CancellationToken);

        _fixture.Users.Users.Single(u => u.Id == result.Value).Role.ShouldBe(Role.Admin);
    }

    [Fact]
    public async Task Handle_Should_RefuseAdminRole_OutsideThePlatformTenant()
    {
        Result<Guid> result = await HandlerFor(_fixture.Admin)
            .HandleAsync(Command(_fixture.OtherTenant.Id.Value, role: Role.Admin), CancellationToken);

        result.Error.ShouldBe(UserErrors.AdminRoleNotAssignable);
    }

    [Fact]
    public async Task Handle_Should_RefuseAdminRole_WhenCallerIsNotAdmin()
    {
        Result<Guid> result = await HandlerFor(_fixture.Manager).HandleAsync(Command(role: Role.Admin), CancellationToken);

        result.Error.ShouldBe(UserErrors.AdminRoleNotAssignable);
        _fixture.UnitOfWork.SaveChangesCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFoundAndCreateNothing_WhenManagerTargetsAnotherTenant()
    {
        int before = _fixture.Users.Users.Count;
        Guid other = _fixture.OtherTenant.Id.Value;

        Result<Guid> result = await HandlerFor(_fixture.Manager).HandleAsync(Command(other), CancellationToken);

        result.Error.ShouldBe(TenantErrors.NotFound(other));
        _fixture.Users.Users.Count.ShouldBe(before);
        _fixture.EmailOutbox.Enqueued.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenEmailIsTakenInAnyTenant()
    {
        User existing = _fixture.AddUser(_fixture.OtherTenant);

        Result<Guid> result = await HandlerFor(_fixture.Manager)
            .HandleAsync(Command(email: existing.Email.ToUpperInvariant()), CancellationToken);

        result.Error.ShouldBe(UserErrors.EmailNotUnique);
        _fixture.UnitOfWork.SaveChangesCount.ShouldBe(0);
    }
}
