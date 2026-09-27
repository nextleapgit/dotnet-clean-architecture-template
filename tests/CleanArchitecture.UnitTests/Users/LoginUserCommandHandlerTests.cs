using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.Login;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Users;

public sealed class LoginUserCommandHandlerTests
{
    private const string Password = "Password123";

    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InMemoryUserStore _userStore = new();
    private readonly InMemoryTenantStore _tenantStore = new();
    private readonly InMemoryRefreshTokenStore _refreshTokenStore = new();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly RecordingAuditLog _auditLog = new();
    private readonly Tenant _tenant = TestData.NewTenant();
    private readonly User _user;

    public LoginUserCommandHandlerTests()
    {
        _tenantStore.Add(_tenant);
        _user = TestData.NewUser(_tenant.Id);
        _userStore.Add(_user);
        _passwordHasher.Verify(Password, _user.PasswordHash).Returns(true);
    }

    private LoginUserCommandHandler Handler => new(
        _unitOfWork,
        _userStore,
        _tenantStore,
        _refreshTokenStore,
        _passwordHasher,
        new FakeTokenProvider(),
        TestData.Clock(),
        _auditLog);

    [Fact]
    public async Task Handle_Should_ReturnInvalidCredentialsHashAndAudit_WhenUserDoesNotExist()
    {
        // Act
        Result<AccessTokensResponse> result = await Handler.HandleAsync(
            new LoginUserCommand("unknown@example.com", Password),
            TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        _passwordHasher.Received(1).Hash(Password);

        AuditRecord audit = _auditLog.Records.ShouldHaveSingleItem();
        audit.Action.ShouldBe(UserAuditActions.LoginFailed);
        audit.Severity.ShouldBe(AuditSeverity.Warning);
        audit.UserId.ShouldBeNull();
        _unitOfWork.SaveChangesCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidCredentialsAndAuditUser_WhenPasswordIsWrong()
    {
        // Act
        Result<AccessTokensResponse> result = await Handler.HandleAsync(
            new LoginUserCommand(_user.Email, "wrong-password"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        _refreshTokenStore.Tokens.ShouldBeEmpty();

        AuditRecord audit = _auditLog.Records.ShouldHaveSingleItem();
        audit.Action.ShouldBe(UserAuditActions.LoginFailed);
        audit.UserId.ShouldBe(_user.Id);
        audit.TenantId.ShouldBe(_user.TenantId);
    }

    [Fact]
    public async Task Handle_Should_ReturnTokensAndPersistOnlyTheRefreshTokenHash_WhenValid()
    {
        // Act
        Result<AccessTokensResponse> result = await Handler.HandleAsync(
            new LoginUserCommand(" TEST@example.COM ", Password),
            TestContext.Current.CancellationToken);

        // Assert
        result.Value.AccessToken.ShouldBe($"access-{_user.Id}");
        result.Value.RefreshToken.ShouldBe("refresh-1");

        RefreshToken stored = _refreshTokenStore.Tokens.ShouldHaveSingleItem();
        stored.TokenHash.ShouldBe("hash(refresh-1)");
        stored.UserId.ShouldBe(_user.Id);
        stored.IsActive(TestData.UtcNow).ShouldBeTrue();

        _auditLog.Records.ShouldHaveSingleItem().Action.ShouldBe(UserAuditActions.LoginSucceeded);
        _unitOfWork.SaveChangesCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnAccountDisabledAndIssueNoToken_WhenUserIsDeactivated()
    {
        // Arrange
        _user.Deactivate();

        // Act
        Result<AccessTokensResponse> result = await Handler.HandleAsync(
            new LoginUserCommand(_user.Email, Password),
            TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(UserErrors.AccountDisabled);
        _refreshTokenStore.Tokens.ShouldBeEmpty();
        _auditLog.Records.ShouldHaveSingleItem().Metadata!["reason"].ShouldBe("account_disabled");
    }

    [Fact]
    public async Task Handle_Should_ReturnAccountDisabled_WhenTenantIsDeactivated()
    {
        // Arrange
        _tenant.Deactivate();

        // Act
        Result<AccessTokensResponse> result = await Handler.HandleAsync(
            new LoginUserCommand(_user.Email, Password),
            TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(UserErrors.AccountDisabled);
        _refreshTokenStore.Tokens.ShouldBeEmpty();
    }
}
