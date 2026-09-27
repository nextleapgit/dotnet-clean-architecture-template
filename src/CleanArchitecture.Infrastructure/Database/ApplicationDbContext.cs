using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Auditing;
using CleanArchitecture.Infrastructure.DomainEvents;
using CleanArchitecture.Infrastructure.Email;
using CleanArchitecture.SharedKernel;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure.Database;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDomainEventsDispatcher domainEventsDispatcher)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Tenant> Tenants { get; set; }

    public DbSet<User> Users { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<UserToken> UserTokens { get; set; }

    public DbSet<TodoItem> TodoItems { get; set; }

    internal DbSet<AuditEntry> AuditEntries { get; set; }

    internal DbSet<EmailOutboxMessage> EmailOutboxMessages { get; set; }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.HasDefaultSchema(Schemas.Default);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<TenantId>().HaveConversion<TenantIdConverter>();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Domain events are published after the save. Inside an explicit transaction they run
        // before the commit; handlers get their own scope, so they must not rely on this context.
        List<IDomainEvent> domainEvents = ExtractDomainEvents();

        int result = await base.SaveChangesAsync(cancellationToken);

        await domainEventsDispatcher.DispatchAsync(domainEvents, cancellationToken);

        return result;
    }

    private List<IDomainEvent> ExtractDomainEvents() =>
        ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .SelectMany(entity =>
            {
                List<IDomainEvent> domainEvents = entity.DomainEvents;

                entity.ClearDomainEvents();

                return domainEvents;
            })
            .ToList();
}
