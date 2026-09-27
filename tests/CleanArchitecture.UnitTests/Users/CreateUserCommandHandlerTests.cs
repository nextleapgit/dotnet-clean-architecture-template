using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.Create;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Users;

public sealed class CreateUserCommandHandlerTests
{
    private readonly UserAdministrationFixture _fixture = new();
    private readonly RecordingEmailOutbox _emailOutbox;
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();

    public CreateUserCommandHandlerTests()
    {
        _emailOutbox = new RecordingEmailOutbox(_fixture.UnitOfWork);
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed");
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private CreateUserCommandHandler HandlerFor(User actor) => new(
        _fixture.UnitOfWork,
        _fixture.TenantAccessFor(actor),
        _fixture.Users,
        _passwordHasher,
        TestData.Clock(),
        _fixture.AuditLog,
        _emailOutbox);

    private static CreateUserCommand Command(Guid? tenantId = null, string email = "new@example.com", Role role = Role.Member) =>
        new(tenantId, email, "New", "User", "Password123", role);

    [Fact]
    public async Task Handle_Should_CreateUserInOwnTenantAtomically_WhenCallerIsManager()
    {
        // Act
        Result<Guid> result = await HandlerFor(_fixture.Manager).HandleAsync(Command(role: Role.Manager), CancellationToken);

        // Assert
        User user = _fixture.Users.Users.Single(u => u.Id == result.Value);
        user.TenantId.ShouldBe(_fixture.Tenant.Id);
        user.Role.ShouldBe(Role.Manager);
        user.PasswordHash.ShouldBe("hashed");

        AuditRecord audit = _fixture.AuditLog.Records.ShouldHaveSingleItem();
        audit.Action.ShouldBe(UserAuditActions.Created);
        audit.EntityId.ShouldBe(user.Id.ToString());
        audit.TenantId.ShouldBe(_fixture.Tenant.Id);

        _emailOutbox.Enqueued.ShouldHaveSingleItem().Message.Recipient.ShouldBe("new@example.com");
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
    public async Task Handle_Should_ReturnNotFoundAndCreateNothing_WhenManagerTargetsAnotherTenant()
    {
        int before = _fixture.Users.Users.Count;
        Guid other = _fixture.OtherTenant.Id.Value;

        Result<Guid> result = await HandlerFor(_fixture.Manager).HandleAsync(Command(other), CancellationToken);

        result.Error.ShouldBe(TenantErrors.NotFound(other));
        _fixture.Users.Users.Count.ShouldBe(before);
        _emailOutbox.Enqueued.ShouldBeEmpty();
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
