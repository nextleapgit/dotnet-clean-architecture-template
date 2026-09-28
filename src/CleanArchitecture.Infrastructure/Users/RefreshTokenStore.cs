using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Users;

internal sealed class RefreshTokenStore(ApplicationDbContext dbContext) : IRefreshTokenStore
{
    public async Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        RefreshToken? token = await dbContext.RefreshTokens
            .Include(rt => rt.User)
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (token is not null)
        {
            await UserRowLock.AcquireAsync(dbContext, token.UserId, cancellationToken);
            await dbContext.Entry(token.User).ReloadAsync(cancellationToken);
            await dbContext.Entry(token).ReloadAsync(cancellationToken);
        }

        return token;
    }

    public async Task<IReadOnlyList<RefreshToken>> GetActiveFamilyAsync(
        Guid familyId,
        DateTime utcNow,
        CancellationToken cancellationToken) =>
        await Active(utcNow)
            .Where(rt => rt.FamilyId == familyId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken) =>
        await Active(utcNow)
            .Where(rt => rt.UserId == userId)
            .ToListAsync(cancellationToken);

    public void Add(RefreshToken refreshToken) => dbContext.RefreshTokens.Add(refreshToken);

    private IQueryable<RefreshToken> Active(DateTime utcNow) =>
        dbContext.RefreshTokens.Where(rt => rt.RevokedAtUtc == null && rt.ExpiresOnUtc > utcNow);
}
