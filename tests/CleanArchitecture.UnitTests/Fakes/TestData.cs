using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.UnitTests.Fakes;

public static class TestData
{
    public static readonly DateTime UtcNow = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    public static IDateTimeProvider Clock(DateTime? utcNow = null)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(utcNow ?? UtcNow);

        return dateTimeProvider;
    }

    public static Tenant NewTenant(string name = "Acme", bool isPlatform = false)
    {
        Tenant tenant = isPlatform ? Tenant.CreatePlatform(name, UtcNow) : Tenant.Create(name, UtcNow);
        tenant.ClearDomainEvents();

        return tenant;
    }

    public static User NewUser(TenantId? tenantId = null, string email = "test@example.com", Role role = Role.Member)
    {
        var user = User.Create(tenantId ?? TenantId.New(), email, "Test", "User", "hash", role);
        user.ClearDomainEvents();

        return user;
    }

    public static TodoItem NewTodo(TenantId tenantId, Guid userId, bool isCompleted = false)
    {
        var todoItem = TodoItem.Create(tenantId, userId, "Existing todo", null, ["home"], Priority.Low, UtcNow);

        if (isCompleted)
        {
            todoItem.Complete(UtcNow);
        }

        todoItem.ClearDomainEvents();

        return todoItem;
    }
}
