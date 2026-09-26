using CleanArchitecture.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.IntegrationTests.Email;

public sealed class EmailOptionsValidationTests
{
    private static SmtpOptionsValidator SmtpValidator(bool outboxEnabled, string environment = "Production") =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["EmailOutbox:Enabled"] = outboxEnabled.ToString() })
                .Build(),
            new HostingEnvironment { EnvironmentName = environment });

    [Theory]
    [InlineData(44, false)]
    [InlineData(45, true)]
    public void Outbox_Should_RequireALeaseCoveringTheWholeAttempt(int leaseSeconds, bool valid)
    {
        var validator = new EmailOutboxOptionsValidator(Options.Create(new SmtpOptions { TimeoutSeconds = 30 }));

        ValidateOptionsResult result = validator.Validate(
            null,
            new EmailOutboxOptions { LeaseSeconds = leaseSeconds, CompletionTimeoutSeconds = 10 });

        result.Succeeded.ShouldBe(valid);
    }

    [Fact]
    public void Smtp_Should_AllowEmptyConnectionSettings_WhenWorkerIsDisabled() =>
        SmtpValidator(outboxEnabled: false).Validate(null, new SmtpOptions()).Succeeded.ShouldBeTrue();

    [Fact]
    public void Smtp_Should_RequireHostAndSender_WhenWorkerIsEnabled()
    {
        ValidateOptionsResult result = SmtpValidator(outboxEnabled: true).Validate(null, new SmtpOptions());

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(f => f.Contains("Smtp:Host", StringComparison.Ordinal));
        result.Failures.ShouldContain(f => f.Contains("Smtp:SenderAddress", StringComparison.Ordinal));
    }

    [Fact]
    public void Smtp_Should_RequireTls_OutsideDevelopment()
    {
        var options = new SmtpOptions
        {
            Host = "smtp.example.com",
            SenderAddress = "no-reply@example.com",
            SecurityMode = SmtpSecurityMode.None
        };

        SmtpValidator(outboxEnabled: true, Environments.Production).Validate(null, options).Failed.ShouldBeTrue();
        SmtpValidator(outboxEnabled: true, Environments.Development).Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Smtp_Should_RequireUserNameAndPasswordTogether() =>
        SmtpValidator(outboxEnabled: false)
            .Validate(null, new SmtpOptions { UserName = "user" })
            .Failed.ShouldBeTrue();
}
