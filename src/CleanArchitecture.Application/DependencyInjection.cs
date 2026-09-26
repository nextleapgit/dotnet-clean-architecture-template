using CleanArchitecture.BuildingBlocks.Cqrs;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services.AddCqrs(typeof(DependencyInjection).Assembly);
}
