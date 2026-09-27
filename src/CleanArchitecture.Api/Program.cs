using System.Reflection;
using CleanArchitecture.Api;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application;
using CleanArchitecture.Infrastructure;
using CleanArchitecture.Infrastructure.Database;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services
    .AddApplication()
    .AddPresentation()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddObservability(builder.Configuration, builder.Environment.ApplicationName);

builder.Services.AddRateLimitingInternal(builder.Configuration);

// REMARK: Only loopback proxies are trusted by default. Add your reverse proxy
// to KnownProxies/KnownNetworks so client IPs (used for rate limiting and audit) are resolved correctly.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

WebApplication app = builder.Build();

app.UseCorrelationId();

app.UseExceptionHandler();

app.UseForwardedHeaders();

app.MapVersionedEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    // REMARK: Development only. Apply production migrations as an explicit deployment step.
    await app.Services.ApplyMigrationsAsync(app.Lifetime.ApplicationStopping);
}

// Creates the first admin from the Bootstrap:Admin section, if configured and no admin exists.
await app.BootstrapAdminAsync(app.Lifetime.ApplicationStopping);

app.MapHealthChecks("health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(CleanArchitecture.Infrastructure.DependencyInjection.ReadinessTag),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.UseSerilogRequestLogging();

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

await app.RunAsync();
