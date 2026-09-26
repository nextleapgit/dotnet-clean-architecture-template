using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.Register;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Users;

public sealed class RegisterUserCommandHandlerTests
{
    private static readonly RegisterUserCommand Command = new(" Test@Example.com ", "Test", "User", "Password123");

    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InMemoryTenantStore _tenantStore = new();
    private readonly InMemoryUserStore _userStore = new();
    private readonly RecordingAuditLog _auditLog = new();
    private readonly RecordingEmailOutbox _emailOutbox;
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();

    public RegisterUserCommandHandlerTests()
    {
        _emailOutbox = new RecordingEmailOutbox(_unitOfWork);
        _passwordHasher.Hash(Command.Password).Returns("hashed-password");
    }

    private RegisterUserCommandHandler Handler => new(
        _unitOfWork,
        _tenantStore,
        _userStore,
        _passwordHasher,
        TestData.Clock(),
        _auditLog,
        _emailOutbox);

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenEmailDiffersOnlyByCase()
    {
        // Arrange
        _userStore.Add(TestData.NewUser(email: "test@example.com"));

        // Act
        Result<Guid> result = await Handler.HandleAsync(Command, TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(UserErrors.EmailNotUnique);
        _tenantStore.Tenants.ShouldBeEmpty();
        _emailOutbox.Enqueued.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_CreateTenantAndUserWithNormalizedEmail_WhenValid()
    {
        // Act
        Result<Guid> result = await Handler.HandleAsync(Command, TestContext.Current.CancellationToken);

        // Assert
        Tenant tenant = _tenantStore.Tenants.ShouldHaveSingleItem();
        User user = _userStore.Users.ShouldHaveSingleItem();
        user.Id.ShouldBe(result.Value);
        user.TenantId.ShouldBe(tenant.Id);
        user.Email.ShouldBe("test@example.com");
        user.PasswordHash.ShouldBe("hashed-password");
        user.DomainEvents.ShouldContain(new UserRegisteredDomainEvent(user.Id));
    }

    [Fact]
    public async Task Handle_Should_AuditAndEnqueueWelcomeEmailInsideCommittedTransaction_WhenValid()
    {
        // Act
        Result<Guid> result = await Handler.HandleAsync(Command, TestContext.Current.CancellationToken);

        // Assert
        _unitOfWork.SavesInsideTransaction.ShouldBe(1);
        _unitOfWork.Committed.ShouldBeTrue();

        AuditRecord audit = _auditLog.Records.ShouldHaveSingleItem();
        audit.Action.ShouldBe(UserAuditActions.Registered);
        audit.EntityId.ShouldBe(result.Value.ToString());
        audit.TenantId.ShouldBe(_tenantStore.Tenants[0].Id);

        (EmailMessage message, DateTime expiresAtUtc) = _emailOutbox.Enqueued.ShouldHaveSingleItem();
        message.Recipient.ShouldBe("test@example.com");
        expiresAtUtc.ShouldBeGreaterThan(TestData.UtcNow);
    }

    [Fact]
    public async Task Handle_Should_HtmlEncodeNamesInWelcomeEmail()
    {
        // Arrange
        RegisterUserCommand command = Command with { FirstName = "<script>" };

        // Act
        await Handler.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        string html = _emailOutbox.Enqueued.ShouldHaveSingleItem().Message.HtmlBody;
        html.ShouldContain("&lt;script&gt;");
        html.ShouldNotContain("<script>");
    }
}
