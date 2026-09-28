using System.Net;
using System.Net.Http.Json;
using System.Xml.Linq;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Email;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;

namespace CleanArchitecture.IntegrationTests.Security;

public sealed class SecurityHardeningTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Logout_Should_RejectTheExistingAccessToken()
    {
        Account account = await CreateAccountAsync();
        Authenticate(account.Tokens.AccessToken);
        using HttpResponseMessage logout = await HttpClient.PostAsJsonAsync(
            "users/logout", new { refreshToken = account.Tokens.RefreshToken }, CancellationToken);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using HttpResponseMessage profile = await HttpClient.GetAsync("users/me", CancellationToken);
        profile.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PasswordChange_Should_InvalidateEarlierLinksAndOtherAccessTokens()
    {
        Account account = await CreateAccountAsync();
        AccessTokens other = await LoginAsync(account.Email);
        using HttpResponseMessage forgot = await HttpClient.PostAsJsonAsync(
            "users/password/forgot", new { email = account.Email }, CancellationToken);
        forgot.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        string resetToken = await ReadEmailedTokenAsync(account.Email, PasswordResetSubject);
        Authenticate(account.Tokens.AccessToken);
        using HttpResponseMessage changed = await HttpClient.PutAsJsonAsync(
            "users/me/password", new { currentPassword = Password, newPassword = "ChangedPassword123!" }, CancellationToken);
        changed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using HttpResponseMessage reset = await HttpClient.PostAsJsonAsync(
            "users/password/reset", new { token = resetToken, newPassword = "ReplacedPassword123!" }, CancellationToken);
        reset.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using HttpResponseMessage current = await HttpClient.GetAsync("users/me", CancellationToken);
        current.StatusCode.ShouldBe(HttpStatusCode.OK);
        Authenticate(other.AccessToken);
        using HttpResponseMessage revoked = await HttpClient.GetAsync("users/me", CancellationToken);
        revoked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetPassword_Should_ImmediatelyRejectExistingAccessTokens()
    {
        Account account = await CreateAccountAsync();
        using HttpResponseMessage forgot = await HttpClient.PostAsJsonAsync(
            "users/password/forgot", new { email = account.Email }, CancellationToken);
        forgot.EnsureSuccessStatusCode();
        string token = await ReadEmailedTokenAsync(account.Email, PasswordResetSubject);
        using HttpResponseMessage reset = await HttpClient.PostAsJsonAsync(
            "users/password/reset", new { token, newPassword = "ResetPassword123!" }, CancellationToken);
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Authenticate(account.Tokens.AccessToken);
        using HttpResponseMessage profile = await HttpClient.GetAsync("users/me", CancellationToken);
        profile.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rotation_Should_KeepAccessValid_UntilReplayRevokesTheFamily()
    {
        Account account = await CreateAccountAsync();
        using HttpResponseMessage rotation = await HttpClient.PostAsJsonAsync(
            "users/refresh-token", new { refreshToken = account.Tokens.RefreshToken }, CancellationToken);
        rotation.EnsureSuccessStatusCode();
        Authenticate(account.Tokens.AccessToken);
        using HttpResponseMessage current = await HttpClient.GetAsync("users/me", CancellationToken);
        current.StatusCode.ShouldBe(HttpStatusCode.OK);
        using HttpResponseMessage replay = await HttpClient.PostAsJsonAsync(
            "users/refresh-token", new { refreshToken = account.Tokens.RefreshToken }, CancellationToken);
        replay.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using HttpResponseMessage revoked = await HttpClient.GetAsync("users/me", CancellationToken);
        revoked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DataProtectionKeys_Should_BeEncrypted_AndReadableByANewHost()
    {
        await CreateAccountAsync();
        string[] keys = await WithDbContextAsync(db => db.DataProtectionKeys.Select(k => k.Xml!).ToArrayAsync(CancellationToken));
        keys.ShouldNotBeEmpty();
        keys.ShouldAllBe(xml => !xml.Contains("<masterKey", StringComparison.Ordinal));
        keys.ShouldContain(xml => xml.Contains("encryptedSecret", StringComparison.Ordinal));
        IDataProtector first = Factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart-test");
        string protectedValue = first.Protect("not-a-secret");
        await using WebApplicationFactory<Program> restarted = Factory.WithWebHostBuilder(_ => { });
        IDataProtector second = restarted.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart-test");
        second.Unprotect(protectedValue).ShouldBe("not-a-secret");
    }

    [Theory]
    [InlineData(999)]
    [InlineData(-1)]
    [InlineData(0)]
    public void Smtp_Should_RejectUnsafeProductionModes(int mode)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["EmailOutbox:Enabled"] = "true" }).Build();
        var validator = new SmtpOptionsValidator(configuration, new HostingEnvironment { EnvironmentName = "Production" });
        validator.Validate(null, new SmtpOptions
        {
            Host = "smtp.example.com", SenderAddress = "no-reply@example.com", SecurityMode = (SmtpSecurityMode)mode
        }).Failed.ShouldBeTrue();
    }

    [Fact]
    public async Task LegacyPlaintextKey_Should_BeWrappedWithoutLosingExistingPayloads()
    {
        var repository = new MemoryKeyRepository();
        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName("CleanArchitecture");
        services.Configure<KeyManagementOptions>(options =>
        {
            options.XmlRepository = repository;
            options.XmlEncryptor = null;
        });
        using ServiceProvider legacy = services.BuildServiceProvider();
        string payload = legacy.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("legacy-upgrade-test").Protect("queued-before-upgrade");
        XElement original = repository.GetAllElements().Single();
        original.ToString().ShouldContain("<masterKey");
        var key = new DataProtectionKey { FriendlyName = "legacy-upgrade-test", Xml = original.ToString() };
        await WithDbContextAsync(async db =>
        {
            db.DataProtectionKeys.Add(key);
            return await db.SaveChangesAsync(CancellationToken);
        });
        try
        {
            await using WebApplicationFactory<Program> upgraded = Factory.WithWebHostBuilder(_ => { });
            upgraded.Services.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("legacy-upgrade-test").Unprotect(payload).ShouldBe("queued-before-upgrade");
            string xml = await WithDbContextAsync(db => db.DataProtectionKeys
                .Where(k => k.Id == key.Id).Select(k => k.Xml!).SingleAsync(CancellationToken));
            xml.ShouldNotContain("<masterKey");
            xml.ShouldContain("encryptedSecret");
            XElement.Parse(xml).Attribute("id")!.Value.ShouldBe(original.Attribute("id")!.Value);
        }
        finally
        {
            await WithDbContextAsync(db => db.DataProtectionKeys.Where(k => k.Id == key.Id)
                .ExecuteDeleteAsync(CancellationToken.None));
        }
    }

    private sealed class MemoryKeyRepository : IXmlRepository
    {
        private readonly List<XElement> _elements = [];
        public IReadOnlyCollection<XElement> GetAllElements() => _elements.Select(e => new XElement(e)).ToArray();
        public void StoreElement(XElement element, string friendlyName) => _elements.Add(new XElement(element));
    }

    [Fact]
    public void Production_Should_RequireAnExternalCertificate()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().Build();
        Should.Throw<InvalidOperationException>(() =>
        {
            using var certificates = new DataProtectionCertificates(configuration, new HostingEnvironment { EnvironmentName = "Production" });
        });
    }

    [Fact]
    public async Task ForbiddenRequests_Should_ConsumeTheGlobalLimit()
    {
        Account account = await CreateAccountAsync();
        await using WebApplicationFactory<Program> limited = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:Global:PermitLimit", "1"));
        using HttpClient client = limited.CreateClient();
        Authenticate(client, account.Tokens.AccessToken);
        using HttpResponseMessage first = await client.GetAsync("/api/v1/tenants", CancellationToken);
        using HttpResponseMessage second = await client.GetAsync("/api/v1/tenants", CancellationToken);
        first.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Kestrel_Should_RejectOversizedRequestBodies()
    {
        await using WebApplicationFactory<Program> server = Factory.WithWebHostBuilder(_ => { });
        server.UseKestrel(0);
        using HttpClient client = server.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/users/login",
            new { email = "test@example.com", password = new string('x', 70_000) }, CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task ConcurrentResetRequests_Should_LeaveOnlyOneUsableLink()
    {
        Account account = await CreateAccountAsync();
        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            HttpClient.PostAsJsonAsync("users/password/forgot", new { email = account.Email }, CancellationToken)));
        foreach (HttpResponseMessage response in responses)
        {
            using (response)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            }
        }
        int count = await WithDbContextAsync(db => db.UserTokens.CountAsync(
            token => token.UserId == account.UserId && token.Purpose == UserTokenPurpose.PasswordReset
                && token.ConsumedAtUtc == null, CancellationToken));
        count.ShouldBe(1);
        string latest = await ReadEmailedTokenAsync(account.Email, PasswordResetSubject);
        using HttpResponseMessage reset = await HttpClient.PostAsJsonAsync(
            "users/password/reset", new { token = latest, newPassword = "AfterConcurrentRequests123!" }, CancellationToken);
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
