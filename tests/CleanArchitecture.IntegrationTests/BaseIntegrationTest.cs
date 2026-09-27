using System.Net.Http.Headers;
using System.Net.Http.Json;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.IntegrationTests;

[Collection(nameof(IntegrationTestCollection))]
public abstract class BaseIntegrationTest
{
    protected const string Password = "Password123";

    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        Factory = factory;
        HttpClient = CreateClient();
    }

    protected IntegrationTestWebAppFactory Factory { get; }

    /// <summary>Relative URLs resolve under /api/v1/; absolute paths (e.g. "/health") reach the root.</summary>
    protected HttpClient HttpClient { get; }

    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public sealed record AccessTokens(string AccessToken, string RefreshToken);

    public sealed record Account(Guid UserId, Guid TenantId, string Email, AccessTokens Tokens);

    protected static string UniqueEmail() => $"test-{Guid.NewGuid():N}@example.com";

    protected HttpClient CreateClient()
    {
        HttpClient client = Factory.CreateClient();
        client.BaseAddress = new Uri(client.BaseAddress!, "api/v1/");

        return client;
    }

    /// <summary>A client signed in as the admin created by the start-up bootstrap.</summary>
    protected async Task<HttpClient> CreateAdminClientAsync()
    {
        HttpClient client = CreateClient();
        Authenticate(client, await Factory.GetAdminAccessTokenAsync(CancellationToken));

        return client;
    }

    protected async Task<Guid> CreateTenantAsync(string? name = null)
    {
        using HttpClient admin = await CreateAdminClientAsync();

        HttpResponseMessage response = await admin.PostAsJsonAsync(
            "tenants",
            new { name = name ?? $"Tenant {Guid.NewGuid():N}" },
            CancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>(CancellationToken);
    }

    /// <summary>Creates a user as the admin — in a new tenant unless one is given.</summary>
    protected async Task<Guid> CreateUserAsync(string email, Role role = Role.Member, Guid? tenantId = null)
    {
        Guid tenant = tenantId ?? await CreateTenantAsync();
        using HttpClient admin = await CreateAdminClientAsync();

        HttpResponseMessage response = await admin.PostAsJsonAsync(
            $"tenants/{tenant}/users",
            new { email, firstName = "Test", lastName = "User", password = Password, role },
            CancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>(CancellationToken);
    }

    protected Task<AccessTokens> LoginAsync(string email) => LoginAsync(HttpClient, email, Password, CancellationToken);

    internal static async Task<AccessTokens> LoginAsync(
        HttpClient client,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "users/login",
            new { email, password },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AccessTokens>(cancellationToken))!;
    }

    /// <summary>Creates a user — in a new tenant unless one is given — and signs in.</summary>
    protected async Task<Account> CreateAccountAsync(Role role = Role.Member, Guid? tenantId = null)
    {
        Guid tenant = tenantId ?? await CreateTenantAsync();
        string email = UniqueEmail();
        Guid userId = await CreateUserAsync(email, role, tenant);

        return new Account(userId, tenant, email, await LoginAsync(email));
    }

    protected void Authenticate(string accessToken) => Authenticate(HttpClient, accessToken);

    protected static void Authenticate(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    protected async Task<T> WithDbContextAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();

        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }
}
