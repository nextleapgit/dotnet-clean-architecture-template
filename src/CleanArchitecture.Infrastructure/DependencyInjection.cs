using System.Text;
using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Abstractions.Links;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.Application.Todos;
using CleanArchitecture.Application.Users;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Infrastructure.Auditing;
using CleanArchitecture.Infrastructure.Authentication;
using CleanArchitecture.Infrastructure.Authorization;
using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.Infrastructure.DomainEvents;
using CleanArchitecture.Infrastructure.Email;
using CleanArchitecture.Infrastructure.Links;
using CleanArchitecture.Infrastructure.Tenancy;
using CleanArchitecture.Infrastructure.Tenants;
using CleanArchitecture.Infrastructure.Time;
using CleanArchitecture.Infrastructure.Todos;
using CleanArchitecture.Infrastructure.Users;
using CleanArchitecture.SharedKernel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CleanArchitecture.Infrastructure;

public static class DependencyInjection
{
    public const string ReadinessTag = "ready";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services
            .AddServices()
            .AddClientLinks(configuration)
            .AddDatabase(configuration)
            .AddHealthChecks(configuration)
            .AddAuthenticationInternal(configuration)
            .AddAuthorizationInternal()
            .AddEmailOutbox(configuration)
            .AddEmailOutboxWorker()
            .AddTokenCleanup(configuration);

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton(TimeProvider.System);

        services.AddTransient<IDomainEventsDispatcher, DomainEventsDispatcher>();

        services.AddHybridCache();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenantContext, CurrentTenantContext>();
        services.AddScoped<IAuditLog, AuditLog>();

        return services;
    }

    private static IServiceCollection AddClientLinks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ClientAppOptions>()
            .Bind(configuration.GetSection(ClientAppOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ClientAppOptions>, ClientAppOptionsValidator>();

        services.AddSingleton<IClientLinks, ClientLinks>();

        return services;
    }

    private static IServiceCollection AddTokenCleanup(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TokenCleanupOptions>()
            .Bind(configuration.GetSection(TokenCleanupOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<TokenCleanupOptions>, TokenCleanupOptionsValidator>();

        services.AddScoped<TokenCleanupStore>();
        services.AddHostedService<TokenCleanupWorker>();

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("Database");

        services.AddDbContext<ApplicationDbContext>(
            options => options
                .UseNpgsql(connectionString, npgsqlOptions =>
                    npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITenantStore, TenantStore>();
        services.AddScoped<IUserStore, UserStore>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IUserTokenStore, UserTokenStore>();
        services.AddScoped<ITodoItemStore, TodoItemStore>();

        return services;
    }

    private static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("Database")!, tags: [ReadinessTag])
            .AddCheck<EmailOutboxHealthCheck>("email-outbox");

        return services;
    }

    private static IServiceCollection AddAuthenticationInternal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.Secret)),
                    ValidIssuer = jwtOptions.Value.Issuer,
                    ValidAudience = jwtOptions.Value.Audience,
                    ClockSkew = TimeSpan.Zero
                });

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenProvider, TokenProvider>();

        return services;
    }

    private static IServiceCollection AddAuthorizationInternal(this IServiceCollection services)
    {
        services.AddAuthorization();

        services.AddScoped<PermissionProvider>();

        services.AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();

        return services;
    }
}
