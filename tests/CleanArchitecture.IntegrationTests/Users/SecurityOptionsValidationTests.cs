using CleanArchitecture.Infrastructure.Links;
using CleanArchitecture.Infrastructure.Users;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.IntegrationTests.Users;

public sealed class SecurityOptionsValidationTests
{
    private static ValidateOptionsResult ValidateClientApp(string configured, string environment) =>
        new ClientAppOptionsValidator(new HostingEnvironment { EnvironmentName = environment })
            .Validate(null, new ClientAppOptions { BaseUrl = configured });

    [Theory]
    [InlineData("https://app.example.com", "Production", true)]
    [InlineData("http://app.example.com", "Production", false)]
    [InlineData("http://localhost:3000", "Development", true)]
    [InlineData("", "Development", false)]
    [InlineData("/relative", "Development", false)]
    [InlineData("ftp://app.example.com", "Development", false)]
    public void ClientApp_Should_RequireAnAbsoluteUrl_AndHttpsOutsideDevelopment(string configured, string environment, bool valid) =>
        ValidateClientApp(configured, environment).Succeeded.ShouldBe(valid);

    [Fact]
    public void TokenCleanup_Should_AcceptTheDefaults() =>
        new TokenCleanupOptionsValidator().Validate(null, new TokenCleanupOptions()).Succeeded.ShouldBeTrue();

    [Fact]
    public void TokenCleanup_Should_RejectNonPositiveValues()
    {
        ValidateOptionsResult result = new TokenCleanupOptionsValidator().Validate(
            null,
            new TokenCleanupOptions { RetentionDays = 0, IntervalMinutes = -1, BatchSize = 0 });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(f => f.Contains("RetentionDays", StringComparison.Ordinal));
        result.Failures.ShouldContain(f => f.Contains("IntervalMinutes", StringComparison.Ordinal));
        result.Failures.ShouldContain(f => f.Contains("BatchSize", StringComparison.Ordinal));
    }
}
