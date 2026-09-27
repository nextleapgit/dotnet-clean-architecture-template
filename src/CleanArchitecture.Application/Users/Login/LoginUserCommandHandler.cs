using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Login;

internal sealed class LoginUserCommandHandler(
    IUnitOfWork unitOfWork,
    IUserStore userStore,
    ITenantStore tenantStore,
    IRefreshTokenStore refreshTokenStore,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    IAuditLog auditLog) : ICommandHandler<LoginUserCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> HandleAsync(
        LoginUserCommand command,
        CancellationToken cancellationToken)
    {
        User? user = await userStore.FindByEmailAsync(User.NormalizeEmail(command.Email), cancellationToken);

        if (user?.PasswordHash is null)
        {
            // Unknown users, and invited users without a password yet: spend the same hashing time
            // as a real verification so response timing does not reveal which emails are registered.
            passwordHasher.Hash(command.Password);

            return await FailAsync(user, user is null ? "unknown_user" : "invitation_pending", UserErrors.InvalidCredentials, cancellationToken);
        }

        // Concurrent attempts on one account run one after another, so each failure is counted and
        // parallel guesses cannot slip past the lockout.
        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        await userStore.LockForUpdateAsync(user, cancellationToken);

        Result<AccessTokensResponse> result = await SignInAsync(user, command.Password, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    private async Task<Result<AccessTokensResponse>> SignInAsync(
        User user,
        string password,
        CancellationToken cancellationToken)
    {
        DateTime utcNow = dateTimeProvider.UtcNow;

        // While locked, the password is not even checked, so guessing gains nothing.
        if (user.IsLockedOut(utcNow))
        {
            return await FailAsync(user, "locked_out", UserErrors.LockedOut, cancellationToken);
        }

        if (user.PasswordHash is null || !passwordHasher.Verify(password, user.PasswordHash))
        {
            if (user.RecordFailedLogin(utcNow, LockoutPolicy.MaxFailedAttempts, LockoutPolicy.Duration))
            {
                auditLog.Record(new AuditRecord(UserAuditActions.AccountLocked, nameof(User), user.Id.ToString(), AuditSeverity.Warning)
                {
                    TenantId = user.TenantId,
                    UserId = user.Id,
                    Metadata = new Dictionary<string, string>
                    {
                        ["lockoutEndUtc"] = user.LockoutEndUtc!.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
                    }
                });
            }

            return await FailAsync(user, "invalid_password", UserErrors.InvalidCredentials, cancellationToken);
        }

        if (!user.IsActive || !await tenantStore.IsActiveAsync(user.TenantId, cancellationToken))
        {
            return await FailAsync(user, "account_disabled", UserErrors.AccountDisabled, cancellationToken);
        }

        user.RecordSuccessfulLogin();

        string refreshToken = tokenProvider.GenerateOpaqueToken();

        var session = RefreshToken.Issue(
            user.Id,
            tokenProvider.HashOpaqueToken(refreshToken),
            utcNow,
            RefreshTokenPolicy.Lifetime);

        refreshTokenStore.Add(session);

        auditLog.Record(new AuditRecord(UserAuditActions.LoginSucceeded, nameof(User), user.Id.ToString())
        {
            TenantId = user.TenantId,
            UserId = user.Id
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AccessTokensResponse(tokenProvider.CreateAccessToken(user, session.FamilyId), refreshToken);
    }

    private async Task<Result<AccessTokensResponse>> FailAsync(
        User? user,
        string reason,
        Error error,
        CancellationToken cancellationToken)
    {
        auditLog.Record(new AuditRecord(
            UserAuditActions.LoginFailed,
            nameof(User),
            user?.Id.ToString(),
            AuditSeverity.Warning)
        {
            TenantId = user?.TenantId,
            UserId = user?.Id,
            Metadata = new Dictionary<string, string> { ["reason"] = reason }
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Failure<AccessTokensResponse>(error);
    }
}
