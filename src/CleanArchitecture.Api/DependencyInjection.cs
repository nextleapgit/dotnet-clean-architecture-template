using CleanArchitecture.Api.Common;
using CleanArchitecture.Api.OpenApi;

namespace CleanArchitecture.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}
