using CleanArchitecture.Application.Users.Bootstrap;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Api.Extensions;

internal static class AdminBootstrapExtensions
{
    public const string SectionName = "Bootstrap:Admin";

    /// <summary>
    /// Creates the platform tenant and first admin from <c>Bootstrap:Admin</c> when no admin exists.
    /// Skipped when the section is absent; fails start-up when it is present but invalid, so a
    /// misconfigured bootstrap is never silently ignored. Needs the schema, so it runs after migrations.
    /// </summary>
    public static async Task BootstrapAdminAsync(this WebApplication app, CancellationToken cancellationToken)
    {
        AdminBootstrapOptions? options = app.Configuration.GetSection(SectionName).Get<AdminBootstrapOptions>();

        if (string.IsNullOrWhiteSpace(options?.Email))
        {
            return;
        }

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();

        ICommandDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        Result result = await dispatcher.DispatchAsync(
            new BootstrapAdminCommand(
                options.TenantName,
                options.Email,
                options.FirstName,
                options.LastName,
                options.Password),
            cancellationToken);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"The '{SectionName}' configuration could not be applied: {result.Error.Code}.");
        }
    }

    private sealed class AdminBootstrapOptions
    {
        public string TenantName { get; init; } = "Platform";

        public string Email { get; init; } = string.Empty;

        public string FirstName { get; init; } = "Platform";

        public string LastName { get; init; } = "Admin";

        public string Password { get; init; } = string.Empty;
    }
}
