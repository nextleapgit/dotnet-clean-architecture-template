using System.Net;
using System.Net.Http.Json;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.IntegrationTests.Users;

public sealed class UserCreationRaceTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task ConcurrentCreations_Should_CreateOneUserAndConflictTheOther()
    {
        // Arrange
        Guid tenantId = await CreateTenantAsync();
        using HttpClient first = await CreateAdminClientAsync();
        using HttpClient second = await CreateAdminClientAsync();
        var request = new { email = UniqueEmail(), firstName = "Test", lastName = "User", password = Password, role = Role.Member };

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(
            first.PostAsJsonAsync($"tenants/{tenantId}/users", request, CancellationToken),
            second.PostAsJsonAsync($"tenants/{tenantId}/users", request, CancellationToken));

        // Assert: whichever request loses — at the up-front check or at the unique index — gets 409.
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
    }

    [Fact]
    public async Task UnitOfWork_Should_TranslateUniqueViolations()
    {
        // Arrange
        string email = UniqueEmail();
        await CreateUserAsync(email);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var tenant = Tenant.Create("Duplicate", DateTime.UtcNow);
        db.Tenants.Add(tenant);
        db.Users.Add(User.Create(tenant.Id, email, "Duplicate", "User", "hash", Role.Member));

        // Act + Assert
        await Should.ThrowAsync<UniqueConstraintViolationException>(() => unitOfWork.SaveChangesAsync(CancellationToken));
    }
}
