using System.Net;
using System.Net.Http.Json;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.IntegrationTests.Users;

public sealed class RegistrationRaceTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task ConcurrentRegistrations_Should_CreateOneUserAndConflictTheOther()
    {
        // Arrange
        var request = new { email = UniqueEmail(), firstName = "Test", lastName = "User", password = Password };

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(
            HttpClient.PostAsJsonAsync("users/register", request, CancellationToken),
            CreateClient().PostAsJsonAsync("users/register", request, CancellationToken));

        // Assert: whichever request loses — at the up-front check or at the unique index — gets 409.
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
    }

    [Fact]
    public async Task UnitOfWork_Should_TranslateUniqueViolations()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var tenant = Tenant.Create("Duplicate", DateTime.UtcNow);
        db.Tenants.Add(tenant);
        db.Users.Add(User.Create(tenant.Id, email, "Duplicate", "User", "hash"));

        // Act + Assert
        await Should.ThrowAsync<UniqueConstraintViolationException>(() => unitOfWork.SaveChangesAsync(CancellationToken));
    }
}
