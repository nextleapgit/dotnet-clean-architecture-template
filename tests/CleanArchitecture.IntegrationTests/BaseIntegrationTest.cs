using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.IntegrationTests;

[Collection(nameof(IntegrationTestCollection))]
public abstract class BaseIntegrationTest
{
    protected const string Password = "Password123";
    protected const string InvitationSubject = "You have been invited";
    protected const string PasswordResetSubject = "Reset your password";

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

    /// <summary>Invites a user as the admin — in a new tenant unless one is given. The user has no password yet.</summary>
    protected async Task<Guid> InviteUserAsync(string email, Role role = Role.Member, Guid? tenantId = null)
    {
        Guid tenant = tenantId ?? await CreateTenantAsync();
        using HttpClient admin = await CreateAdminClientAsync();

        HttpResponseMessage response = await admin.PostAsJsonAsync(
            $"tenants/{tenant}/users",
            new { email, firstName = "Test", lastName = "User", role },
            CancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>(CancellationToken);
    }

    /// <summary>Invites a user and accepts the invitation with <see cref="Password"/>, as the user would.</summary>
    protected async Task<Guid> CreateUserAsync(string email, Role role = Role.Member, Guid? tenantId = null)
    {
        Guid userId = await InviteUserAsync(email, role, tenantId);
        string token = await ReadEmailedTokenAsync(email, InvitationSubject);

        HttpResponseMessage accept = await HttpClient.PostAsJsonAsync(
            "users/invitations/accept",
            new { token, password = Password },
            CancellationToken);
        accept.EnsureSuccessStatusCode();

        return userId;
    }

    /// <summary>
    /// The token from the newest pending email with this recipient and subject — read from the
    /// outbox, as the user would read it from their mailbox. The worker is off in tests.
    /// </summary>
    protected async Task<string> ReadEmailedTokenAsync(string recipient, string subject)
    {
        EmailPayloadProtector protector = Factory.Services.GetRequiredService<EmailPayloadProtector>();

        List<EmailOutboxMessage> pending = await WithDbContextAsync(db => db.EmailOutboxMessages.AsNoTracking()
            .Where(m => m.Status == EmailOutboxStatus.Pending)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(CancellationToken));

        EmailMessage email = pending
            .Select(row => protector.Unprotect(row.Id, row.ExpiresAtUtc, row.Payload!))
            .Where(result => result.IsSuccess)
            .Select(result => result.Value)
            .First(message => message.Recipient == recipient && message.Subject == subject);

        Match link = Regex.Match(email.TextBody, "token=([^\\s]+)", RegexOptions.None, TimeSpan.FromSeconds(1));
        link.Success.ShouldBeTrue();

        return Uri.UnescapeDataString(link.Groups[1].Value);
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
