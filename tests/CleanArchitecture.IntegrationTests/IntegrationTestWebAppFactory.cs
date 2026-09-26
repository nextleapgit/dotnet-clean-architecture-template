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

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("clean-architecture")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", _dbContainer.GetConnectionString());

        // Deterministic JWT settings so tokens can be issued and validated in tests.
        builder.UseSetting("Jwt:Secret", JwtSecret);
        builder.UseSetting("Jwt:Issuer", "clean-architecture");
        builder.UseSetting("Jwt:Audience", "developers");
        builder.UseSetting("Jwt:ExpirationInMinutes", "60");

        // Relax rate limiting so the test suite is not throttled.
        builder.UseSetting("RateLimiting:Global:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Authentication:PermitLimit", "100000");

        // Outbox tests drive the dispatcher directly; the worker runs only where a test enables it.
        builder.UseSetting("EmailOutbox:Enabled", "false");
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
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
