using System.Security.Cryptography;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.IntegrationTests.Users;

/// <summary>
/// Token timestamps are set in the past, so the cleanup (which uses the database clock) sees them
/// as old. Tokens of other tests are recent and therefore never deleted.
/// </summary>
public sealed class TokenCleanupTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const int RetentionDays = 30;

    private static readonly DateTime UtcNow = DateTime.UtcNow;

    private static string NewHash() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private async Task SaveAsync(params object[] tokens) =>
        await WithDbContextAsync(async db =>
        {
            db.AddRange(tokens);
            return await db.SaveChangesAsync(CancellationToken);
        });

    private async Task RunCleanupAsync()
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        TokenCleanupStore store = scope.ServiceProvider.GetRequiredService<TokenCleanupStore>();

        // One batch is enough: the batch size exceeds every token the test suite creates.
        await store.DeleteEndedRefreshTokenFamiliesAsync(100_000, RetentionDays, CancellationToken);
        await store.DeleteEndedUserTokensAsync(100_000, RetentionDays, CancellationToken);
    }

    private Task<bool> RefreshTokenExistsAsync(Guid id) =>
        WithDbContextAsync(db => db.RefreshTokens.AnyAsync(t => t.Id == id, CancellationToken));

    private Task<bool> UserTokenExistsAsync(Guid id) =>
        WithDbContextAsync(db => db.UserTokens.AnyAsync(t => t.Id == id, CancellationToken));

    [Fact]
    public async Task Cleanup_Should_DeleteRefreshTokenFamily_When_EveryTokenEndedBeforeRetention()
    {
        // Arrange: a session rotated once, then logged out — all 60 days ago.
        Guid userId = await InviteUserAsync(UniqueEmail());
        var first = RefreshToken.Issue(userId, NewHash(), UtcNow.AddDays(-61), TimeSpan.FromDays(7));
        RefreshToken second = first.Rotate(NewHash(), UtcNow.AddDays(-60), TimeSpan.FromDays(7));
        second.Revoke(UtcNow.AddDays(-60));
        await SaveAsync(first, second);

        // Act
        await RunCleanupAsync();

        // Assert
        (await RefreshTokenExistsAsync(first.Id)).ShouldBeFalse();
        (await RefreshTokenExistsAsync(second.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Cleanup_Should_KeepOldRotatedToken_While_ItsFamilyHasAnActiveToken()
    {
        // Arrange: the old token was rotated long ago, but its successor is still usable —
        // replaying the old one must still be detected as reuse.
        Guid userId = await InviteUserAsync(UniqueEmail());
        var rotated = RefreshToken.Issue(userId, NewHash(), UtcNow.AddDays(-90), TimeSpan.FromDays(7));
        RefreshToken active = rotated.Rotate(NewHash(), UtcNow.AddDays(-89), TimeSpan.FromDays(100));
        await SaveAsync(rotated, active);

        // Act
        await RunCleanupAsync();

        // Assert
        (await RefreshTokenExistsAsync(rotated.Id)).ShouldBeTrue();
        (await RefreshTokenExistsAsync(active.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task Cleanup_Should_KeepRefreshTokenFamily_When_ItEndedWithinRetention()
    {
        // Arrange: expired 10 days ago, revoked 5 days ago.
        Guid userId = await InviteUserAsync(UniqueEmail());
        var expired = RefreshToken.Issue(userId, NewHash(), UtcNow.AddDays(-17), TimeSpan.FromDays(7));
        var revoked = RefreshToken.Issue(userId, NewHash(), UtcNow.AddDays(-6), TimeSpan.FromDays(7));
        revoked.Revoke(UtcNow.AddDays(-5));
        await SaveAsync(expired, revoked);

        // Act
        await RunCleanupAsync();

        // Assert
        (await RefreshTokenExistsAsync(expired.Id)).ShouldBeTrue();
        (await RefreshTokenExistsAsync(revoked.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task Cleanup_Should_DeleteUserTokens_When_UsedOrExpiredBeforeRetention()
    {
        // Arrange
        Guid userId = await InviteUserAsync(UniqueEmail());
        var used = UserToken.Issue(userId, UserTokenPurpose.PasswordReset, NewHash(), UtcNow.AddDays(-40), TimeSpan.FromHours(1));
        used.Consume(UtcNow.AddDays(-40));
        var expired = UserToken.Issue(userId, UserTokenPurpose.Invitation, NewHash(), UtcNow.AddDays(-40), TimeSpan.FromDays(3));
        await SaveAsync(used, expired);

        // Act
        await RunCleanupAsync();

        // Assert
        (await UserTokenExistsAsync(used.Id)).ShouldBeFalse();
        (await UserTokenExistsAsync(expired.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Cleanup_Should_KeepUserTokens_When_UsableOrEndedWithinRetention()
    {
        // Arrange: an old but still valid token, and one used recently.
        Guid userId = await InviteUserAsync(UniqueEmail());
        var usable = UserToken.Issue(userId, UserTokenPurpose.Invitation, NewHash(), UtcNow.AddDays(-40), TimeSpan.FromDays(60));
        var recentlyUsed = UserToken.Issue(userId, UserTokenPurpose.PasswordReset, NewHash(), UtcNow.AddDays(-2), TimeSpan.FromHours(1));
        recentlyUsed.Consume(UtcNow.AddDays(-2));
        await SaveAsync(usable, recentlyUsed);

        // Act
        await RunCleanupAsync();

        // Assert
        (await UserTokenExistsAsync(usable.Id)).ShouldBeTrue();
        (await UserTokenExistsAsync(recentlyUsed.Id)).ShouldBeTrue();
    }
}
