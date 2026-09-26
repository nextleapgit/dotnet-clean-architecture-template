using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.GetById;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Users;

public sealed class GetUserByIdQueryHandlerTests
{
    private readonly InMemoryUserStore _store = new();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserBelongsToAnotherTenant()
    {
        // Arrange
        User foreign = TestData.NewUser();
        _store.Add(foreign);
        var handler = new GetUserByIdQueryHandler(_store, new FakeTenantContext(TenantId.New(), Guid.NewGuid()));

        // Act
        Result<UserResponse> result = await handler.HandleAsync(
            new GetUserByIdQuery(foreign.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(UserErrors.NotFound(foreign.Id));
    }

    [Fact]
    public async Task Handle_Should_ReturnUser_WhenInSameTenant()
    {
        // Arrange
        User user = TestData.NewUser();
        _store.Add(user);
        var handler = new GetUserByIdQueryHandler(_store, new FakeTenantContext(user.TenantId, Guid.NewGuid()));

        // Act
        Result<UserResponse> result = await handler.HandleAsync(
            new GetUserByIdQuery(user.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.Value.Id.ShouldBe(user.Id);
        result.Value.TenantId.ShouldBe(user.TenantId.Value);
    }
}
