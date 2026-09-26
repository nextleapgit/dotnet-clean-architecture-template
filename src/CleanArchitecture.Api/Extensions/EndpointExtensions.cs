using System.Reflection;
using CleanArchitecture.Api.Endpoints;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CleanArchitecture.Api.Extensions;

public static class EndpointExtensions
{
    public const string CurrentVersionPrefix = "api/v1";

    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        ServiceDescriptor[] serviceDescriptors = assembly
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
            .ToArray();

        services.TryAddEnumerable(serviceDescriptors);

        return services;
    }

    /// <summary>Maps every endpoint under /api/v1. Breaking changes go to a new /api/v2 group.</summary>
    public static IApplicationBuilder MapVersionedEndpoints(this WebApplication app)
    {
        RouteGroupBuilder versionGroup = app.MapGroup(CurrentVersionPrefix);

        foreach (IEndpoint endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint.MapEndpoint(versionGroup);
        }

        return app;
    }

    public static RouteHandlerBuilder HasPermission(this RouteHandlerBuilder app, string permission) =>
        app.RequireAuthorization(permission);
}
