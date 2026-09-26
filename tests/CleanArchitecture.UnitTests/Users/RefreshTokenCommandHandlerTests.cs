using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.Refresh;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Users;

public sealed class RefreshTokenCommandHandlerTests
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InMemoryRefreshTokenStore _store = new();
    private readonly RecordingAuditLog _auditLog = new();
    private readonly FakeTokenProvider _tokenProvider = new();
    private readonly User _user = TestData.NewUser();

    public RefreshTokenCommandHandlerTests() => _store.KnowUser(_user);

    private RefreshTokenCommandHandler Handler => new(_unitOfWork, _store, _tokenProvider, TestData.Clock(), _auditLog);

    private RefreshToken IssueStoredToken(string rawToken, DateTime createdAtUtc)
    {
        var token = RefreshToken.Issue(_user.Id, _tokenProvider.HashRefreshToken(rawToken), createdAtUtc, Lifetime);
        _store.Add(token);

        return token;
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidRefreshToken_WhenTokenIsUnknown()
    {
        // Act
        Result<AccessTokensResponse> result = await Handler.HandleAsync(
            new RefreshTokenCommand("unknown"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidRefreshToken_WhenTokenIsExpired()
    {
        // Arrange
        IssueStoredToken("old", TestData.UtcNow.AddDays(-8));

        // Act
        Result<AccessTokensResponse> result = await Handler.HandleAsync(
            new RefreshTokenCommand("old"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
        _store.Tokens.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_RejectWithoutReuseAlarm_WhenTokenWasRevokedByLogout()
    {
        // Arrange
        RefreshToken token = IssueStoredToken("logged-out", TestData.UtcNow.AddHours(-1));
        token.Revoke(TestData.UtcNow.AddMinutes(-5));

        // Act
        Result<AccessTokensResponse> result = await Handler.HandleAsync(
            new RefreshTokenCommand("logged-out"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
        _auditLog.Records.ShouldBeEmpty();
        _unitOfWork.SaveChangesCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_RotateWithinFamilyAndRevokeOldToken_WhenValid()
    {
        // Arrange
        RefreshToken original = IssueStoredToken("current", TestData.UtcNow.AddHours(-1));

        // Act
        Result<AccessTokensResponse> result = await Handler.HandleAsync(
            new RefreshTokenCommand("current"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Value.AccessToken.ShouldBe($"access-{_user.Id}");

        RefreshToken successor = _store.Tokens.Single(t => t.Id != original.Id);
        successor.TokenHash.ShouldBe(_tokenProvider.HashRefreshToken(result.Value.RefreshToken));
        successor.FamilyId.ShouldBe(original.FamilyId);
        original.IsRevoked.ShouldBeTrue();
        original.ReplacedByTokenId.ShouldBe(successor.Id);

        _auditLog.Records.ShouldHaveSingleItem().Action.ShouldBe(UserAuditActions.RefreshTokenRotated);
    }

    [Fact]
    public async Task Handle_Should_RevokeWholeFamilyAndAuditCritical_WhenRotatedTokenIsReused()
    {
        // Arrange
        IssueStoredToken("stolen", TestData.UtcNow.AddHours(-1));
        Result<AccessTokensResponse> firstUse = await Handler.HandleAsync(
            new RefreshTokenCommand("stolen"),
            TestContext.Current.CancellationToken);
        _auditLog.Records.Clear();

        // Act
        Result<AccessTokensResponse> reuse = await Handler.HandleAsync(
            new RefreshTokenCommand("stolen"),
            TestContext.Current.CancellationToken);

        // Assert
        firstUse.IsSuccess.ShouldBeTrue();
        reuse.Error.ShouldBe(UserErrors.InvalidRefreshToken);
        _store.Tokens.ShouldAllBe(t => t.IsRevoked);

        AuditRecord audit = _auditLog.Records.ShouldHaveSingleItem();
        audit.Action.ShouldBe(UserAuditActions.RefreshTokenReuseDetected);
        audit.Severity.ShouldBe(AuditSeverity.Critical);
    }
}
