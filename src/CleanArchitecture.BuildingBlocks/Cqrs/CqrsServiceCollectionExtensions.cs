using System.Reflection;
using CleanArchitecture.BuildingBlocks.Cqrs.Behaviors;
using CleanArchitecture.SharedKernel;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.BuildingBlocks.Cqrs;

public static class CqrsServiceCollectionExtensions
{
    /// <summary>
    /// Registers every handler, validator, and domain event handler in <paramref name="assembly"/>
    /// as scoped services, wraps handlers with validation and logging, and adds the dispatchers.
    /// </summary>
    public static IServiceCollection AddCqrs(this IServiceCollection services, Assembly assembly)
    {
        services.Scan(scan => scan.FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(IDomainEventHandler<>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime());

        // Decoration order: validation runs inside logging, so rejected requests are logged too.
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(ValidationDecorator.QueryHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator.CommandHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<>), typeof(ValidationDecorator.CommandBaseHandler<>));

        services.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingDecorator.QueryHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(LoggingDecorator.CommandHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<>), typeof(LoggingDecorator.CommandBaseHandler<>));

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();

        return services;
    }
}
