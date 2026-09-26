using CleanArchitecture.Domain.Users;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Domain;

public sealed class RefreshTokenTests
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    [Fact]
    public void Issue_Should_CreateActiveTokenInNewFamily()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", TestData.UtcNow, Lifetime);

        token.IsActive(TestData.UtcNow).ShouldBeTrue();
        token.ExpiresOnUtc.ShouldBe(TestData.UtcNow.Add(Lifetime));
        token.FamilyId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void IsActive_Should_BeFalse_AfterExpiry() =>
        RefreshToken.Issue(Guid.NewGuid(), "hash", TestData.UtcNow, Lifetime)
            .IsActive(TestData.UtcNow.Add(Lifetime))
            .ShouldBeFalse();

    [Fact]
    public void Rotate_Should_RevokeCurrentAndLinkSuccessorInSameFamily()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash-1", TestData.UtcNow, Lifetime);

        RefreshToken successor = token.Rotate("hash-2", TestData.UtcNow.AddMinutes(5), Lifetime);

        token.IsRevoked.ShouldBeTrue();
        token.WasRotated.ShouldBeTrue();
        token.ReplacedByTokenId.ShouldBe(successor.Id);
        successor.FamilyId.ShouldBe(token.FamilyId);
        successor.UserId.ShouldBe(token.UserId);
        successor.IsActive(TestData.UtcNow.AddMinutes(5)).ShouldBeTrue();
    }

    [Fact]
    public void Revoke_Should_KeepTheFirstRevocationTime()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", TestData.UtcNow, Lifetime);

        token.Revoke(TestData.UtcNow.AddMinutes(1));
        token.Revoke(TestData.UtcNow.AddMinutes(2));

        token.RevokedAtUtc.ShouldBe(TestData.UtcNow.AddMinutes(1));
        token.WasRotated.ShouldBeFalse();
    }
}
