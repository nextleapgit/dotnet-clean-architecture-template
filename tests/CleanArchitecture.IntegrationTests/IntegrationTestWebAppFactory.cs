using CleanArchitecture.Api;
using CleanArchitecture.Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CleanArchitecture.IntegrationTests;

public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtSecret = "super-duper-secret-value-that-should-be-in-user-secrets";
    public const string AdminEmail = "admin@integration.test";
    public const string AdminPassword = "AdminPassword123";

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("clean-architecture")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly SemaphoreSlim _adminTokenLock = new(1, 1);
    private string? _adminAccessToken;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", _dbContainer.GetConnectionString());

        // Deterministic JWT settings so tokens can be issued and validated in tests.
        builder.UseSetting("Jwt:Secret", JwtSecret);
        builder.UseSetting("Jwt:Issuer", "clean-architecture");
        builder.UseSetting("Jwt:Audience", "developers");
        builder.UseSetting("Jwt:ExpirationInMinutes", "60");

        // The platform admin every test uses to create tenants and users (self sign-up does not exist).
        builder.UseSetting("Bootstrap:Admin:TenantName", "Platform");
        builder.UseSetting("Bootstrap:Admin:Email", AdminEmail);
        builder.UseSetting("Bootstrap:Admin:Password", AdminPassword);

        builder.UseSetting("ClientApp:BaseUrl", "https://app.integration.test");

        // Relax rate limiting so the test suite is not throttled.
        builder.UseSetting("RateLimiting:Global:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Authentication:PermitLimit", "100000");

        // Outbox tests drive the dispatcher directly; the worker runs only where a test enables it.
        builder.UseSetting("EmailOutbox:Enabled", "false");
    }

    /// <summary>Signs the admin in once per test run; the token outlives the suite.</summary>
    public async Task<string> GetAdminAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _adminTokenLock.WaitAsync(cancellationToken);

        try
        {
            if (_adminAccessToken is null)
            {
                using HttpClient client = CreateClient();
                client.BaseAddress = new Uri(client.BaseAddress!, "api/v1/");

                _adminAccessToken = (await BaseIntegrationTest.LoginAsync(client, AdminEmail, AdminPassword, cancellationToken))
                    .AccessToken;
            }

            return _adminAccessToken;
        }
        finally
        {
            _adminTokenLock.Release();
        }
    }

    public async ValueTask InitializeAsync()
    {
        await _dbContainer.StartAsync();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        _adminTokenLock.Dispose();
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
