using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.Infrastructure.Database;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Email;

internal static class EmailServiceCollectionExtensions
{
    /// <summary>Enqueue, storage, and dispatch — usable by tools and tests without a running worker.</summary>
    public static IServiceCollection AddEmailOutbox(this IServiceCollection services, IConfiguration configuration)
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
        services.AddDataProtection()
            .SetApplicationName("CleanArchitecture")
            .PersistKeysToDbContext<ApplicationDbContext>();

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
