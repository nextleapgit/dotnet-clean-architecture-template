using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Authentication;

/// <summary>
/// An access token is only as valid as its session: once the refresh-token family behind its
/// <c>session_id</c> claim is revoked (logout, password reset, detected reuse) or expired, the token
/// fails authentication — a 401, so clients know to sign in again — even before it expires.
/// </summary>
internal sealed class SessionValidator(ApplicationDbContext dbContext, IDateTimeProvider dateTimeProvider)
{
    public Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        DateTime utcNow = dateTimeProvider.UtcNow;

        return dbContext.RefreshTokens.AnyAsync(
            token => token.UserId == userId
                && token.FamilyId == sessionId
                && token.RevokedAtUtc == null
                && token.ExpiresOnUtc > utcNow,
            cancellationToken);
    }
}
