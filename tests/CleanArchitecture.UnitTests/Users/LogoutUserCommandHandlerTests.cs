using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.Logout;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Users;

public sealed class LogoutUserCommandHandlerTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InMemoryRefreshTokenStore _store = new();
    private readonly RecordingAuditLog _auditLog = new();
    private readonly FakeTokenProvider _tokenProvider = new();
    private readonly User _user = TestData.NewUser();

    private LogoutUserCommandHandler HandlerFor(Guid currentUserId) => new(
        _unitOfWork,
        _store,
        _tokenProvider,
        new FakeTenantContext(_user.TenantId, currentUserId),
        TestData.Clock(),
        _auditLog);

    private RefreshToken IssueStoredToken(string rawToken)
    {
        var token = RefreshToken.Issue(
            _user.Id,
            _tokenProvider.HashRefreshToken(rawToken),
            TestData.UtcNow.AddHours(-1),
            TimeSpan.FromDays(7));
        _store.Add(token);

        return token;
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidRefreshToken_WhenTokenBelongsToAnotherUser()
    {
        // Arrange
        RefreshToken token = IssueStoredToken("theirs");

        // Act
        Result result = await HandlerFor(Guid.NewGuid()).HandleAsync(
            new LogoutUserCommand("theirs"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
        token.IsRevoked.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_RevokeTokenAndAudit_WhenTokenBelongsToCurrentUser()
    {
        // Arrange
        RefreshToken token = IssueStoredToken("mine");

        // Act
        Result result = await HandlerFor(_user.Id).HandleAsync(
            new LogoutUserCommand("mine"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        token.IsRevoked.ShouldBeTrue();
        _auditLog.Records.ShouldHaveSingleItem().Action.ShouldBe(UserAuditActions.LoggedOut);
        _unitOfWork.SaveChangesCount.ShouldBe(1);
    }
}
