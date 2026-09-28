using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.Infrastructure.Database;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace CleanArchitecture.Infrastructure.Email;

internal static class EmailServiceCollectionExtensions
{
    /// <summary>Enqueue, storage, and dispatch — usable by tools and tests without a running worker.</summary>
    public static IServiceCollection AddEmailOutbox(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<EmailOutboxOptions>()
            .Bind(configuration.GetSection(EmailOutboxOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<EmailOutboxOptions>, EmailOutboxOptionsValidator>();

        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();

        // Every instance must share one key ring, or payloads queued by one cannot be sent by another.
        // The factory registration transfers ownership to DI; the upgrade hosted service resolves it.
#pragma warning disable CA2000
        var certificates = new DataProtectionCertificates(configuration, environment);
#pragma warning restore CA2000
        services.AddSingleton(_ => certificates);
        services.AddDataProtection()
            .SetApplicationName("CleanArchitecture")
            .PersistKeysToDbContext<ApplicationDbContext>()
            .ProtectKeysWithCertificate(certificates.Active)
            .UnprotectKeysWithAnyCertificate(certificates.All);
        services.AddHostedService<DataProtectionKeyEncryption>();

        services.AddSingleton<EmailPayloadProtector>();
        services.AddScoped<IEmailOutbox, EmailOutbox>();
        services.AddScoped<EmailOutboxStore>();
        services.AddScoped<EmailOutboxDispatcher>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        return services;
    }

    public static IServiceCollection AddEmailOutboxWorker(this IServiceCollection services)
    {
        services.AddHostedService<EmailOutboxWorker>();

        return services;
    }
}
