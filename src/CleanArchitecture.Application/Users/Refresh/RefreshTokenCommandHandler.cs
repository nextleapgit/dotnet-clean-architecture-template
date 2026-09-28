using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Refresh;

internal sealed class RefreshTokenCommandHandler(
    IUnitOfWork unitOfWork,
    IRefreshTokenStore refreshTokenStore,
    ITenantStore tenantStore,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    IAuditLog auditLog) : ICommandHandler<RefreshTokenCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> HandleAsync(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        RefreshToken? refreshToken = await refreshTokenStore.FindByHashAsync(
            tokenProvider.HashOpaqueToken(command.RefreshToken),
            cancellationToken);

        if (refreshToken is null)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        DateTime utcNow = dateTimeProvider.UtcNow;

        if (refreshToken.WasRotated)
        {
            await RevokeFamilyAfterReuseAsync(refreshToken, utcNow, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        if (!refreshToken.IsActive(utcNow))
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        // Deactivation revokes a user's sessions, but a tenant's deactivation does not: check both.
        if (!refreshToken.User.IsActive
            || !await tenantStore.IsActiveAsync(refreshToken.User.TenantId, cancellationToken))
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.AccountDisabled);
        }

        string newRefreshToken = tokenProvider.GenerateOpaqueToken();

        RefreshToken successor = refreshToken.Rotate(
            tokenProvider.HashOpaqueToken(newRefreshToken),
            utcNow,
            RefreshTokenPolicy.Lifetime);

        refreshTokenStore.Add(successor);

        auditLog.Record(new AuditRecord(UserAuditActions.RefreshTokenRotated, nameof(User), refreshToken.UserId.ToString())
        {
            TenantId = refreshToken.User.TenantId,
            UserId = refreshToken.UserId
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new AccessTokensResponse(tokenProvider.CreateAccessToken(refreshToken.User, successor.FamilyId), newRefreshToken);
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
