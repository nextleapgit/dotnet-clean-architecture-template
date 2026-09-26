using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Email;

internal enum SmtpSecurityMode
{
    None = 0,
    StartTls = 1,
    SslOnConnect = 2
}

/// <summary>Credentials belong in user secrets or a secret store, never in tracked configuration.</summary>
internal sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public SmtpSecurityMode SecurityMode { get; init; } = SmtpSecurityMode.StartTls;
    public string SenderAddress { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string? UserName { get; init; }
    public string? Password { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
}

// Reads the outbox switch from configuration: depending on IOptions<EmailOutboxOptions> would be circular,
// because the outbox validator depends on these SMTP options.
internal sealed class SmtpOptionsValidator(IConfiguration configuration, IHostEnvironment environment)
    : IValidateOptions<SmtpOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpOptions options)
    {
        List<string> failures = [];

        if (options.Port is < 1 or > 65535)
        {
            failures.Add("Smtp:Port must be between 1 and 65535.");
        }

        if (options.TimeoutSeconds <= 0)
        {
            failures.Add("Smtp:TimeoutSeconds must be positive.");
        }

        if (string.IsNullOrEmpty(options.UserName) != string.IsNullOrEmpty(options.Password))
        {
            failures.Add("Smtp:UserName and Smtp:Password must be configured together.");
        }

        // Connection settings are only mandatory once the worker will actually send.
        if (configuration.GetValue<bool>($"{EmailOutboxOptions.SectionName}:{nameof(EmailOutboxOptions.Enabled)}"))
        {
            if (string.IsNullOrWhiteSpace(options.Host))
            {
                failures.Add("Smtp:Host is required when EmailOutbox:Enabled is true.");
            }

            if (!MailAddress.TryCreate(options.SenderAddress, out _))
            {
                failures.Add("Smtp:SenderAddress must be a valid address when EmailOutbox:Enabled is true.");
            }

            if (options.SecurityMode == SmtpSecurityMode.None && !environment.IsDevelopment())
            {
                failures.Add("Smtp:SecurityMode must be StartTls or SslOnConnect outside Development.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
