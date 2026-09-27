using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using CleanArchitecture.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.IntegrationTests.Api;

public sealed class ApiConventionTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly string[] UnversionedRoutes = ["health", "health/ready", "openapi/{documentName}.json"];

    private static readonly string[] KnownPermissions = typeof(Permissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (string)field.GetValue(null)!)
        .ToArray();

    private static string Route(RouteEndpoint endpoint) => endpoint.RoutePattern.RawText!.TrimStart('/');

    private IEnumerable<RouteEndpoint> ApplicationEndpoints() =>
        Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => !UnversionedRoutes.Contains(Route(endpoint))
                               && !Route(endpoint).StartsWith("scalar", StringComparison.Ordinal));

    [Fact]
    public void Endpoints_Should_BeVersioned()
    {
        var endpoints = ApplicationEndpoints().ToList();

        endpoints.ShouldNotBeEmpty();
        endpoints.Select(Route).ShouldAllBe(route => route.StartsWith("api/v1/", StringComparison.Ordinal));
    }

    [Fact]
    public void Endpoints_Should_BeAnonymousOrRequireAKnownPermission()
    {
        string[] violations = ApplicationEndpoints()
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null &&
                !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => KnownPermissions.Contains(a.Policy)))
            .Select(Route)
            .ToArray();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void OnlyAuthenticationEndpoints_Should_BeAnonymous() =>
        ApplicationEndpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(Route)
            .ShouldBe(
                [
                    "api/v1/users/login",
                    "api/v1/users/refresh-token",
                    "api/v1/users/invitations/accept",
                    "api/v1/users/password/forgot",
                    "api/v1/users/password/reset"
                ],
                ignoreOrder: true);

    [Fact]
    public async Task OpenApiDocument_Should_DescribeBearerAuthAndVersionedRoutes()
    {
        string document = await HttpClient.GetStringAsync("/openapi/v1.json", CancellationToken);

        document.ShouldContain("\"bearer\"");
        document.ShouldContain("/api/v1/todos");
    }

    [Fact]
    public async Task Scalar_Should_BeTheDocumentationUi()
    {
        HttpResponseMessage scalar = await HttpClient.GetAsync("/scalar", CancellationToken);
        HttpResponseMessage swagger = await HttpClient.GetAsync("/swagger", CancellationToken);

        scalar.IsSuccessStatusCode.ShouldBeTrue();
        swagger.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Problems_Should_CarryStableCodeAndCorrelationId()
    {
        // Arrange
        Account account = await CreateAccountAsync();
        Authenticate(account.Tokens.AccessToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"todos/{Guid.NewGuid()}");
        request.Headers.Add("Correlation-Id", "client-correlation-42");

        // Act
        HttpResponseMessage response = await HttpClient.SendAsync(request, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Headers.GetValues("Correlation-Id").ShouldBe(["client-correlation-42"]);
        Todos.ProblemBody problem = (await response.Content.ReadFromJsonAsync<Todos.ProblemBody>(CancellationToken))!;
        problem.Code.ShouldBe("TodoItems.NotFound");
        problem.CorrelationId.ShouldBe("client-correlation-42");
    }

    [Fact]
    public async Task CorrelationId_Should_IgnoreUnsafeClientValues()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("Correlation-Id", "<script>alert(1)</script>");

        HttpResponseMessage response = await HttpClient.SendAsync(request, CancellationToken);

        response.Headers.GetValues("Correlation-Id").Single().ShouldNotContain("<");
    }

    [Fact]
    public async Task ReadinessProbe_Should_BeHealthy() =>
        (await HttpClient.GetAsync("/health/ready", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

    [Fact]
    public void Startup_Should_Fail_WhenJwtSecretIsTooShort()
    {
        using WebApplicationFactory<Program> misconfigured = Factory.WithWebHostBuilder(builder => builder.UseSetting("Jwt:Secret", "too-short"));

        Should.Throw<OptionsValidationException>(() => misconfigured.CreateClient());
    }
}
