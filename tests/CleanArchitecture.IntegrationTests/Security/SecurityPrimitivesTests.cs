using System.Security.Claims;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Authentication;
using CleanArchitecture.Infrastructure.Email;
using CleanArchitecture.Infrastructure.Tenancy;
using CleanArchitecture.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CleanArchitecture.IntegrationTests.Security;

/// <summary>Infrastructure security building blocks, tested without a database.</summary>
public sealed class SecurityPrimitivesTests
{
    private static readonly DateTime FixedNow = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime UtcNow => FixedNow;
    }

    private static TokenProvider CreateTokenProvider() =>
        new(
            Options.Create(new JwtOptions
            {
                Secret = IntegrationTestWebAppFactory.JwtSecret,
                Issuer = "issuer",
                Audience = "audience",
                ExpirationInMinutes = 15
            }),
            new FixedClock());

    [Fact]
    public void PasswordHasher_Should_VerifyOnlyTheOriginalPassword()
    {
        var hasher = new PasswordHasher();

        string hash = hasher.Hash("Correct horse battery staple");

        hasher.Verify("Correct horse battery staple", hash).ShouldBeTrue();
        hasher.Verify("correct horse battery staple", hash).ShouldBeFalse();
        hasher.Hash("Correct horse battery staple").ShouldNotBe(hash); // random salt
    }

    [Fact]
    public void AccessToken_Should_CarryUserTenantAndExpiryFromTheClock()
    {
        var user = User.Create(TenantId.New(), "user@example.com", "Test", "User", "hash");

        string token = CreateTokenProvider().CreateAccessToken(user);
        JsonWebToken jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        jwt.Subject.ShouldBe(user.Id.ToString());
        jwt.GetClaim("tenant_id").Value.ShouldBe(user.TenantId.ToString());
        jwt.GetClaim("email").Value.ShouldBe("user@example.com");
        jwt.Issuer.ShouldBe("issuer");
        jwt.ValidTo.ShouldBe(FixedNow.AddMinutes(15));
    }

    [Fact]
    public void RefreshTokens_Should_BeRandomAndHashedDeterministically()
    {
        TokenProvider provider = CreateTokenProvider();

        string first = provider.GenerateRefreshToken();
        string second = provider.GenerateRefreshToken();

        first.ShouldNotBe(second);
        Convert.FromBase64String(first).Length.ShouldBe(32);
        provider.HashRefreshToken(first).ShouldBe(provider.HashRefreshToken(first));
        provider.HashRefreshToken(first).Length.ShouldBe(RefreshToken.TokenHashLength);
        provider.HashRefreshToken(first).ShouldNotContain(first);
    }

    [Fact]
    public void TenantContext_Should_ResolveTenantAndUserFromAnAuthenticatedPrincipal()
    {
        var tenantId = TenantId.New();
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim("tenant_id", tenantId.ToString())],
            authenticationType: "Test"));

        var context = new CurrentTenantContext(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });

        context.IsAvailable.ShouldBeTrue();
        context.CurrentTenantId.ShouldBe(tenantId);
        context.CurrentUserId.ShouldBe(userId);
        context.AccessibleTenantIds.ShouldBe([tenantId]);
    }

    [Fact]
    public void TenantContext_Should_BeUnavailableForAnonymousRequests()
    {
        var context = new CurrentTenantContext(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        context.IsAvailable.ShouldBeFalse();
        context.AccessibleTenantIds.ShouldBeEmpty();
        Should.Throw<TenantContextUnavailableException>(() => context.CurrentTenantId);
    }

    [Theory]
    [InlineData(1, 15, 30)]
    [InlineData(3, 60, 120)]
    [InlineData(20, 1800, 3600)]
    public void RetryDelay_Should_GrowExponentiallyWithJitterAndStayCapped(int attempt, int minimum, int maximum)
    {
        for (int i = 0; i < 50; i++)
        {
            RetryDelay.Seconds(attempt, baseDelaySeconds: 30, maxDelaySeconds: 3600).ShouldBeInRange(minimum, maximum);
        }
    }
}
