using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.EmailChange;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Users;

public sealed class EmailChangeCommandHandlerTests
{
    private const string NewEmail = "new-address@example.com";

    private readonly UserAdministrationFixture _fixture = new();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();

    public EmailChangeCommandHandlerTests()
    {
        _passwordHasher.Verify("Current123", "hash").Returns(true);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private RequestEmailChangeCommandHandler RequestHandler(User actor) => new(
        _fixture.UnitOfWork, _fixture.Users, _fixture.TokenIssuer(), _passwordHasher, _fixture.ClientLinks,
        _fixture.ContextOf(actor), TestData.Clock(), _fixture.AuditLog, _fixture.EmailOutbox);

    private ConfirmEmailChangeCommandHandler ConfirmHandler => new(
        _fixture.UnitOfWork, _fixture.Users, _fixture.TokenIssuer(), _fixture.Tenants, _fixture.RefreshTokens,
        TestData.Clock(), _fixture.AuditLog);

    private RefreshToken AddSession(User user)
    {
        var session = RefreshToken.Issue(user.Id, $"hash-{Guid.NewGuid():N}", TestData.UtcNow, TimeSpan.FromDays(7));
        _fixture.RefreshTokens.Add(session);

        return session;
    }

    // ------------------------------------------------------------ request

    [Fact]
    public async Task Request_Should_EmailALinkToTheNewAddress_AndANoticeToTheCurrentOne()
    {
        User user = _fixture.AddUser(_fixture.Tenant);
        string currentEmail = user.Email;

        Result result = await RequestHandler(user)
            .HandleAsync(new RequestEmailChangeCommand("Current123", "  New-Address@Example.com "), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.Email.ShouldBe(currentEmail);

        UserToken token = _fixture.UserTokens.Tokens.ShouldHaveSingleItem();
        token.Purpose.ShouldBe(UserTokenPurpose.EmailChange);
        token.Payload.ShouldBe(NewEmail);

        _fixture.EmailOutbox.Enqueued.Count.ShouldBe(2);
        _fixture.EmailOutbox.Enqueued.ShouldContain(e => e.Message.Recipient == NewEmail
            && e.Message.TextBody.Contains("https://app.test/confirm-email?token=", StringComparison.Ordinal));
        _fixture.EmailOutbox.Enqueued.ShouldContain(e => e.Message.Recipient == currentEmail
            && !e.Message.TextBody.Contains("token=", StringComparison.Ordinal));

        AuditRecord audit = _fixture.AuditLog.Records.ShouldHaveSingleItem();
        audit.Action.ShouldBe(UserAuditActions.EmailChangeRequested);
        audit.Metadata.ShouldBeNull();
        _fixture.UnitOfWork.Committed.ShouldBeTrue();
    }

    [Fact]
    public async Task Request_Should_AnswerTheSame_ButSendNoLink_WhenTheAddressBelongsToAnotherAccount()
    {
        User user = _fixture.AddUser(_fixture.Tenant);
        User other = _fixture.AddUser(_fixture.Tenant);

        Result result = await RequestHandler(user)
            .HandleAsync(new RequestEmailChangeCommand("Current123", other.Email), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _fixture.UserTokens.Tokens.ShouldBeEmpty();
        _fixture.EmailOutbox.Enqueued.ShouldHaveSingleItem().Message.Recipient.ShouldBe(user.Email);
    }

    [Fact]
    public async Task Request_Should_FailAndCountTowardsLockout_WhenCurrentPasswordIsWrong()
    {
        User user = _fixture.AddUser(_fixture.Tenant);

        Result result = await RequestHandler(user)
            .HandleAsync(new RequestEmailChangeCommand("Wrong123", NewEmail), CancellationToken);

        result.Error.ShouldBe(UserErrors.InvalidCurrentPassword);
        user.FailedLoginAttempts.ShouldBe(1);
        _fixture.UserTokens.Tokens.ShouldBeEmpty();
        _fixture.EmailOutbox.Enqueued.ShouldBeEmpty();
    }

    [Fact]
    public async Task Request_Should_Fail_WhenTheAccountIsLockedOut()
    {
        User user = _fixture.AddUser(_fixture.Tenant);
        user.RecordFailedLogin(TestData.UtcNow, 1, TimeSpan.FromMinutes(15));

        Result result = await RequestHandler(user)
            .HandleAsync(new RequestEmailChangeCommand("Current123", NewEmail), CancellationToken);

        result.Error.ShouldBe(UserErrors.LockedOut);
        _fixture.EmailOutbox.Enqueued.ShouldBeEmpty();
    }

    [Fact]
    public async Task Request_Should_Fail_WhenTheNewAddressIsTheCurrentOne()
    {
        User user = _fixture.AddUser(_fixture.Tenant);

        Result result = await RequestHandler(user)
            .HandleAsync(new RequestEmailChangeCommand("Current123", user.Email.ToUpperInvariant()), CancellationToken);

        result.Error.ShouldBe(UserErrors.EmailUnchanged);
        _fixture.EmailOutbox.Enqueued.ShouldBeEmpty();
    }

    [Fact]
    public async Task Request_Should_SupersedeAnEarlierLink()
    {
        User user = _fixture.AddUser(_fixture.Tenant);
        string earlier = _fixture.IssueToken(user, UserTokenPurpose.EmailChange, payload: "earlier@example.com");

        await RequestHandler(user).HandleAsync(new RequestEmailChangeCommand("Current123", NewEmail), CancellationToken);

        (await _fixture.TokenIssuer().FindUsableAsync(earlier, UserTokenPurpose.EmailChange, CancellationToken)).ShouldBeNull();
        _fixture.UserTokens.Tokens.Count(t => t.IsUsable(TestData.UtcNow)).ShouldBe(1);
    }

    // ------------------------------------------------------------ confirm

    [Fact]
    public async Task Confirm_Should_ChangeTheEmail_EndEverySession_AndInvalidateResetLinks()
    {
        User user = _fixture.AddUser(_fixture.Tenant);
        RefreshToken session = AddSession(user);
        string resetLink = _fixture.IssueToken(user, UserTokenPurpose.PasswordReset);
        string token = _fixture.IssueToken(user, UserTokenPurpose.EmailChange, payload: NewEmail);

        Result result = await ConfirmHandler.HandleAsync(new ConfirmEmailChangeCommand(token), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.Email.ShouldBe(NewEmail);
        session.IsRevoked.ShouldBeTrue();
        (await _fixture.TokenIssuer().FindUsableAsync(resetLink, UserTokenPurpose.PasswordReset, CancellationToken)).ShouldBeNull();
        (await ConfirmHandler.HandleAsync(new ConfirmEmailChangeCommand(token), CancellationToken))
            .Error.ShouldBe(UserErrors.InvalidOrExpiredToken);

        AuditRecord audit = _fixture.AuditLog.Records.ShouldHaveSingleItem();
        audit.Action.ShouldBe(UserAuditActions.EmailChanged);
        audit.Severity.ShouldBe(AuditSeverity.Warning);
        audit.Metadata!["revokedSessions"].ShouldBe("1");
        _fixture.UnitOfWork.Committed.ShouldBeTrue();
    }

    [Fact]
    public async Task Confirm_Should_ReturnConflict_WhenTheAddressWasTakenMeanwhile()
    {
        User user = _fixture.AddUser(_fixture.Tenant);
        User other = _fixture.AddUser(_fixture.Tenant);
        string token = _fixture.IssueToken(user, UserTokenPurpose.EmailChange, payload: other.Email);

        Result result = await ConfirmHandler.HandleAsync(new ConfirmEmailChangeCommand(token), CancellationToken);

        result.Error.ShouldBe(UserErrors.EmailNotUnique);
        user.Email.ShouldNotBe(other.Email);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("wrong-purpose")]
    [InlineData("no-address")]
    [InlineData("unknown")]
    [InlineData("deactivated-user")]
    [InlineData("deactivated-tenant")]
    public async Task Confirm_Should_ReturnTheSameError_ForEveryUnusableLink(string reason)
    {
        User user = _fixture.AddUser(_fixture.Tenant);
        string currentEmail = user.Email;
        string token = reason switch
        {
            "expired" => _fixture.IssueToken(user, UserTokenPurpose.EmailChange, TimeSpan.Zero, NewEmail),
            "wrong-purpose" => _fixture.IssueToken(user, UserTokenPurpose.PasswordReset, payload: NewEmail),
            "no-address" => _fixture.IssueToken(user, UserTokenPurpose.EmailChange),
            "unknown" => "not-a-token",
            _ => _fixture.IssueToken(user, UserTokenPurpose.EmailChange, payload: NewEmail)
        };

        if (reason == "deactivated-user")
        {
            user.Deactivate();
        }
        else if (reason == "deactivated-tenant")
        {
            _fixture.Tenant.Deactivate();
        }

        Result result = await ConfirmHandler.HandleAsync(new ConfirmEmailChangeCommand(token), CancellationToken);

        result.Error.ShouldBe(UserErrors.InvalidOrExpiredToken);
        user.Email.ShouldBe(currentEmail);
    }
}
