using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Infrastructure.Database;

public static class MigrationExtensions
{
    /// <summary>
    /// Applies pending migrations. Development only — production schema changes are an explicit
    /// deployment step (for example an EF migration bundle), never an application start-up side effect.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
