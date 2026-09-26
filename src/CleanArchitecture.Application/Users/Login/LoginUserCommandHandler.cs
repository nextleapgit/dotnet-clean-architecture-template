using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Login;

internal sealed class LoginUserCommandHandler(
    IUnitOfWork unitOfWork,
    IUserStore userStore,
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

        if (user is null)
        {
            // Spend the same hashing time as a real verification so response timing
            // does not reveal which emails are registered.
            passwordHasher.Hash(command.Password);

            return await FailAsync(null, "unknown_user", cancellationToken);
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            return await FailAsync(user, "invalid_password", cancellationToken);
        }

        string refreshToken = tokenProvider.GenerateRefreshToken();

        refreshTokenStore.Add(RefreshToken.Issue(
            user.Id,
            tokenProvider.HashRefreshToken(refreshToken),
            dateTimeProvider.UtcNow,
            RefreshTokenPolicy.Lifetime));

        auditLog.Record(new AuditRecord(UserAuditActions.LoginSucceeded, nameof(User), user.Id.ToString())
        {
            TenantId = user.TenantId,
            UserId = user.Id
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AccessTokensResponse(tokenProvider.CreateAccessToken(user), refreshToken);
    }

    private async Task<Result<AccessTokensResponse>> FailAsync(
        User? user,
        string reason,
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

        return Result.Failure<AccessTokensResponse>(UserErrors.InvalidCredentials);
    }
}
