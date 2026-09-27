using CleanArchitecture.Application.Tenants;
using CleanArchitecture.Application.Users;
using CleanArchitecture.BuildingBlocks.Cqrs;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<TenantAccess>();
        services.AddScoped<UserManagement>();
        services.AddScoped<UserTokenIssuer>();

        return services.AddCqrs(typeof(DependencyInjection).Assembly);
    }
}
