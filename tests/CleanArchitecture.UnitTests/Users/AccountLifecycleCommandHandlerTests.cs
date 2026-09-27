using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.Invitations;
using CleanArchitecture.Application.Users.Passwords;
using CleanArchitecture.Application.Users.Unlock;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Users;

public sealed class AccountLifecycleCommandHandlerTests
{
    private const string NewPassword = "NewPassword123";

    private readonly UserAdministrationFixture _fixture = new();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();

    public AccountLifecycleCommandHandlerTests()
    {
        _passwordHasher.Hash(Arg.Any<string>()).Returns(call => $"hashed({call.Arg<string>()})");
        _passwordHasher.Verify("Current123", "hash").Returns(true);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private User InvitedUser()
    {
        var user = User.Create(_fixture.Tenant.Id, $"invited-{Guid.NewGuid():N}@example.com", "In", "Vited", null, Role.Member);
        user.ClearDomainEvents();
        _fixture.Users.Add(user);

        return user;
    }

    private RefreshToken AddSession(User user)
    {
        var session = RefreshToken.Issue(user.Id, $"hash-{Guid.NewGuid():N}", TestData.UtcNow, TimeSpan.FromDays(7));
        _fixture.RefreshTokens.Add(session);

        return session;
    }

    // ------------------------------------------------------------ accept invitation

    private AcceptInvitationCommandHandler AcceptHandler => new(
        _fixture.UnitOfWork, _fixture.TokenIssuer(), _fixture.Tenants, _passwordHasher, TestData.Clock(), _fixture.AuditLog);

    [Fact]
    public async Task AcceptInvitation_Should_SetThePasswordAndConsumeTheToken()
    {
        User user = InvitedUser();
        string token = _fixture.IssueToken(user, UserTokenPurpose.Invitation);

        Result result = await AcceptHandler.HandleAsync(new AcceptInvitationCommand(token, NewPassword), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.PasswordHash.ShouldBe($"hashed({NewPassword})");
        _fixture.UserTokens.Tokens.ShouldHaveSingleItem().IsUsable(TestData.UtcNow).ShouldBeFalse();
        _fixture.AuditLog.Records.ShouldHaveSingleItem().Action.ShouldBe(UserAuditActions.InvitationAccepted);
    }

    [Fact]
    public async Task AcceptInvitation_Should_Fail_WhenTheTokenWasAlreadyUsed()
    {
        User user = InvitedUser();
        string token = _fixture.IssueToken(user, UserTokenPurpose.Invitation);
        await AcceptHandler.HandleAsync(new AcceptInvitationCommand(token, NewPassword), CancellationToken);

        Result second = await AcceptHandler.HandleAsync(new AcceptInvitationCommand(token, "Another123"), CancellationToken);

        second.Error.ShouldBe(UserErrors.InvalidOrExpiredToken);
        user.PasswordHash.ShouldBe($"hashed({NewPassword})");
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("wrong-purpose")]
    [InlineData("unknown")]
    [InlineData("deactivated-user")]
    [InlineData("deactivated-tenant")]
    public async Task AcceptInvitation_Should_ReturnTheSameError_ForEveryUnusableLink(string reason)
    {
        User user = InvitedUser();
        string token = reason switch
        {
            "expired" => _fixture.IssueToken(user, UserTokenPurpose.Invitation, TimeSpan.Zero),
            "wrong-purpose" => _fixture.IssueToken(user, UserTokenPurpose.PasswordReset),
            "unknown" => "not-a-token",
            _ => _fixture.IssueToken(user, UserTokenPurpose.Invitation)
        };

        if (reason == "deactivated-user")
        {
            user.Deactivate();
        }
        else if (reason == "deactivated-tenant")
        {
            _fixture.Tenant.Deactivate();
        }

        Result result = await AcceptHandler.HandleAsync(new AcceptInvitationCommand(token, NewPassword), CancellationToken);

        result.Error.ShouldBe(UserErrors.InvalidOrExpiredToken);
        user.HasPassword.ShouldBeFalse();
    }

    // ------------------------------------------------------------ resend invitation

    private ResendInvitationCommandHandler ResendHandler(User actor) => new(
        _fixture.UnitOfWork, _fixture.UserManagementFor(actor), _fixture.TokenIssuer(), _fixture.ClientLinks, _fixture.AuditLog, _fixture.EmailOutbox);

    [Fact]
    public async Task ResendInvitation_Should_SupersedeThePreviousLink()
    {
        User user = InvitedUser();
        string first = _fixture.IssueToken(user, UserTokenPurpose.Invitation);

        Result result = await ResendHandler(_fixture.Manager).HandleAsync(new ResendInvitationCommand(null, user.Id), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _fixture.UserTokens.Tokens.Count(t => t.IsUsable(TestData.UtcNow)).ShouldBe(1);
        (await _fixture.TokenIssuer().FindUsableAsync(first, UserTokenPurpose.Invitation, CancellationToken)).ShouldBeNull();
        _fixture.EmailOutbox.Enqueued.ShouldHaveSingleItem().Message.Recipient.ShouldBe(user.Email);
        _fixture.UnitOfWork.Committed.ShouldBeTrue();
    }

    [Fact]
    public async Task ResendInvitation_Should_Fail_WhenTheUserAlreadyHasAPassword()
    {
        User member = _fixture.AddUser(_fixture.Tenant);

        Result result = await ResendHandler(_fixture.Manager).HandleAsync(new ResendInvitationCommand(null, member.Id), CancellationToken);

        result.Error.ShouldBe(UserErrors.InvitationAlreadyAccepted);
        _fixture.EmailOutbox.Enqueued.ShouldBeEmpty();
    }

    // ------------------------------------------------------------ change password

    private ChangePasswordCommandHandler ChangeHandler(User actor, Guid? sessionId) => new(
        _fixture.UnitOfWork, _fixture.Users, _fixture.RefreshTokens, _passwordHasher, _fixture.ContextOf(actor, sessionId),
        TestData.Clock(), _fixture.AuditLog, _fixture.EmailOutbox);

    [Fact]
    public async Task ChangePassword_Should_KeepTheCurrentSessionAndRevokeTheOthers()
    {
        User user = _fixture.AddUser(_fixture.Tenant);
        RefreshToken current = AddSession(user);
        RefreshToken other = AddSession(user);

        Result result = await ChangeHandler(user, current.FamilyId)
            .HandleAsync(new ChangePasswordCommand("Current123", NewPassword), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.PasswordHash.ShouldBe($"hashed({NewPassword})");
        current.IsRevoked.ShouldBeFalse();
        other.IsRevoked.ShouldBeTrue();

        AuditRecord audit = _fixture.AuditLog.Records.ShouldHaveSingleItem();
        audit.Action.ShouldBe(UserAuditActions.PasswordChanged);
        audit.Metadata!["revokedSessions"].ShouldBe("1");
        _fixture.EmailOutbox.Enqueued.ShouldHaveSingleItem().Message.Subject.ShouldBe("Your password was changed");
        _fixture.UnitOfWork.Committed.ShouldBeTrue();
    }

    [Fact]
    public async Task ChangePassword_Should_FailAndCountTowardsLockout_WhenCurrentPasswordIsWrong()
    {
        User user = _fixture.AddUser(_fixture.Tenant);

        Result result = await ChangeHandler(user, null).HandleAsync(new ChangePasswordCommand("Wrong123", NewPassword), CancellationToken);

        result.Error.ShouldBe(UserErrors.InvalidCurrentPassword);
        user.PasswordHash.ShouldBe("hash");
        user.FailedLoginAttempts.ShouldBe(1);
        _fixture.EmailOutbox.Enqueued.ShouldBeEmpty();
    }

    // ------------------------------------------------------------ forgot / reset password

    private ForgotPasswordCommandHandler ForgotHandler => new(
        _fixture.UnitOfWork, _fixture.Users, _fixture.Tenants, _fixture.TokenIssuer(), _fixture.ClientLinks, _fixture.AuditLog, _fixture.EmailOutbox);

    private ResetPasswordCommandHandler ResetHandler => new(
        _fixture.UnitOfWork, _fixture.TokenIssuer(), _fixture.Tenants, _fixture.RefreshTokens, _passwordHasher, TestData.Clock(), _fixture.AuditLog);

    [Fact]
    public async Task ForgotPassword_Should_EmailAResetLink_WhenTheAccountCanSignIn()
    {
        User user = _fixture.AddUser(_fixture.Tenant);

        Result result = await ForgotHandler.HandleAsync(new ForgotPasswordCommand(user.Email.ToUpperInvariant()), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _fixture.UserTokens.Tokens.ShouldHaveSingleItem().Purpose.ShouldBe(UserTokenPurpose.PasswordReset);
        _fixture.EmailOutbox.Enqueued.ShouldHaveSingleItem().Message.TextBody.ShouldContain("https://app.test/reset-password?token=");
        _fixture.AuditLog.Records.ShouldHaveSingleItem().Action.ShouldBe(UserAuditActions.PasswordResetRequested);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("invited")]
    [InlineData("deactivated")]
    public async Task ForgotPassword_Should_SucceedWithoutAnEmail_WhenTheAccountCannotSignIn(string state)
    {
        string email = state switch
        {
            "unknown" => "nobody@example.com",
            "invited" => InvitedUser().Email,
            _ => DeactivatedUser().Email
        };

        Result result = await ForgotHandler.HandleAsync(new ForgotPasswordCommand(email), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _fixture.EmailOutbox.Enqueued.ShouldBeEmpty();
        _fixture.UserTokens.Tokens.ShouldBeEmpty();
    }

    private User DeactivatedUser()
    {
        User user = _fixture.AddUser(_fixture.Tenant);
        user.Deactivate();

        return user;
    }

    [Fact]
    public async Task ResetPassword_Should_SetThePasswordLiftTheLockoutAndRevokeEverySession()
    {
        User user = _fixture.AddUser(_fixture.Tenant);
        user.RecordFailedLogin(TestData.UtcNow, 1, TimeSpan.FromMinutes(15));
        RefreshToken session = AddSession(user);
        string token = _fixture.IssueToken(user, UserTokenPurpose.PasswordReset);

        Result result = await ResetHandler.HandleAsync(new ResetPasswordCommand(token, NewPassword), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.PasswordHash.ShouldBe($"hashed({NewPassword})");
        user.IsLockedOut(TestData.UtcNow).ShouldBeFalse();
        session.IsRevoked.ShouldBeTrue();
        (await ResetHandler.HandleAsync(new ResetPasswordCommand(token, "Again12345"), CancellationToken))
            .Error.ShouldBe(UserErrors.InvalidOrExpiredToken);
    }

    [Fact]
    public async Task ResetPassword_Should_RejectAnInvitationToken()
    {
        User user = InvitedUser();
        string token = _fixture.IssueToken(user, UserTokenPurpose.Invitation);

        Result result = await ResetHandler.HandleAsync(new ResetPasswordCommand(token, NewPassword), CancellationToken);

        result.Error.ShouldBe(UserErrors.InvalidOrExpiredToken);
    }

    // ------------------------------------------------------------ unlock

    [Fact]
    public async Task Unlock_Should_LiftTheLockoutAndAudit()
    {
        User user = _fixture.AddUser(_fixture.Tenant);
        user.RecordFailedLogin(TestData.UtcNow, 1, TimeSpan.FromMinutes(15));
        var handler = new UnlockUserCommandHandler(_fixture.UnitOfWork, _fixture.UserManagementFor(_fixture.Manager), _fixture.AuditLog);

        Result result = await handler.HandleAsync(new UnlockUserCommand(null, user.Id), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.IsLockedOut(TestData.UtcNow).ShouldBeFalse();
        _fixture.AuditLog.Records.ShouldHaveSingleItem().Action.ShouldBe(UserAuditActions.Unlocked);
    }
}
