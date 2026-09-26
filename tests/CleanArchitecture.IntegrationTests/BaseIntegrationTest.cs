using System.Net.Http.Headers;
using System.Net.Http.Json;
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

    protected sealed record AccessTokens(string AccessToken, string RefreshToken);

    protected sealed record Account(Guid UserId, string Email, AccessTokens Tokens);

    protected static string UniqueEmail() => $"test-{Guid.NewGuid():N}@example.com";

    protected HttpClient CreateClient()
    {
        HttpClient client = Factory.CreateClient();
        client.BaseAddress = new Uri(client.BaseAddress!, "api/v1/");

        return client;
    }

    protected async Task<Guid> RegisterUserAsync(string email)
    {
        var request = new { email, firstName = "Test", lastName = "User", password = Password };

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/register", request, CancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>(CancellationToken);
    }

    protected async Task<AccessTokens> LoginAsync(string email)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/login",
            new { email, password = Password },
            CancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AccessTokens>(CancellationToken))!;
    }

    /// <summary>Registers a new user — and therefore a new tenant — and signs in.</summary>
    protected async Task<Account> RegisterAndLoginAsync()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);

        return new Account(userId, email, await LoginAsync(email));
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
