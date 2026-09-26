using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Users;

internal sealed class RefreshTokenStore(ApplicationDbContext dbContext) : IRefreshTokenStore
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens
            .Include(rt => rt.User)
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveFamilyAsync(
        Guid familyId,
        DateTime utcNow,
        CancellationToken cancellationToken) =>
        await dbContext.RefreshTokens
            .Where(rt => rt.FamilyId == familyId && rt.RevokedAtUtc == null && rt.ExpiresOnUtc > utcNow)
            .ToListAsync(cancellationToken);

    public void Add(RefreshToken refreshToken) => dbContext.RefreshTokens.Add(refreshToken);
}
