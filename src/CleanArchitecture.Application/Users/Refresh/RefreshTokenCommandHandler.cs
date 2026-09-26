using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Refresh;

internal sealed class RefreshTokenCommandHandler(
    IUnitOfWork unitOfWork,
    IRefreshTokenStore refreshTokenStore,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    IAuditLog auditLog) : ICommandHandler<RefreshTokenCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> HandleAsync(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        RefreshToken? refreshToken = await refreshTokenStore.FindByHashAsync(
            tokenProvider.HashRefreshToken(command.RefreshToken),
            cancellationToken);

        if (refreshToken is null)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        DateTime utcNow = dateTimeProvider.UtcNow;

        if (refreshToken.WasRotated)
        {
            await RevokeFamilyAfterReuseAsync(refreshToken, utcNow, cancellationToken);

            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        if (!refreshToken.IsActive(utcNow))
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        string newRefreshToken = tokenProvider.GenerateRefreshToken();

        RefreshToken successor = refreshToken.Rotate(
            tokenProvider.HashRefreshToken(newRefreshToken),
            utcNow,
            RefreshTokenPolicy.Lifetime);

        refreshTokenStore.Add(successor);

        auditLog.Record(new AuditRecord(UserAuditActions.RefreshTokenRotated, nameof(User), refreshToken.UserId.ToString())
        {
            TenantId = refreshToken.User.TenantId,
            UserId = refreshToken.UserId
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AccessTokensResponse(tokenProvider.CreateAccessToken(refreshToken.User), newRefreshToken);
    }

    // A rotated token can only be presented again if it leaked: end every session in its family.
    private async Task RevokeFamilyAfterReuseAsync(
        RefreshToken reusedToken,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<RefreshToken> family = await refreshTokenStore.GetActiveFamilyAsync(
            reusedToken.FamilyId,
            utcNow,
            cancellationToken);

        foreach (RefreshToken token in family)
        {
            token.Revoke(utcNow);
        }

        auditLog.Record(new AuditRecord(
            UserAuditActions.RefreshTokenReuseDetected,
            nameof(User),
            reusedToken.UserId.ToString(),
            AuditSeverity.Critical)
        {
            TenantId = reusedToken.User.TenantId,
            UserId = reusedToken.UserId,
            Metadata = new Dictionary<string, string>
            {
                ["familyId"] = reusedToken.FamilyId.ToString(),
                ["revokedTokens"] = family.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
