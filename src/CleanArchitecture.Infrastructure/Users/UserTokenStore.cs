using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Users;

internal sealed class UserTokenStore(ApplicationDbContext dbContext) : IUserTokenStore
{
    public Task LockUserAsync(Guid userId, CancellationToken cancellationToken) =>
        UserRowLock.AcquireAsync(dbContext, userId, cancellationToken);

    public async Task<UserToken?> FindByHashAsync(string tokenHash, UserTokenPurpose purpose, CancellationToken cancellationToken)
    {
        UserToken? token = await dbContext.UserTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash && t.Purpose == purpose, cancellationToken);

        if (token is not null)
        {
            await LockUserAsync(token.UserId, cancellationToken);
            await dbContext.Entry(token.User).ReloadAsync(cancellationToken);
            await dbContext.Entry(token).ReloadAsync(cancellationToken);
        }

        return token;
    }

    public async Task<IReadOnlyList<UserToken>> GetUsableAsync(
        Guid userId,
        UserTokenPurpose purpose,
        DateTime utcNow,
        CancellationToken cancellationToken) =>
        await dbContext.UserTokens
            .Where(t => t.UserId == userId && t.Purpose == purpose && t.ConsumedAtUtc == null && t.ExpiresAtUtc > utcNow)
            .ToListAsync(cancellationToken);

    public void Add(UserToken userToken) => dbContext.UserTokens.Add(userToken);
}
