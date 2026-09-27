using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.GetCurrent;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Users;

public sealed class GetCurrentUserQueryHandlerTests
{
    private readonly InMemoryUserStore _store = new();

    [Fact]
    public async Task Handle_Should_ReturnCurrentUser_WhenUserExists()
    {
        // Arrange
        User user = TestData.NewUser();
        _store.Add(user);
        var handler = new GetCurrentUserQueryHandler(_store, new FakeTenantContext(user.TenantId, user.Id));

        // Act
        Result<UserResponse> result = await handler.HandleAsync(
            new GetCurrentUserQuery(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Value.Id.ShouldBe(user.Id);
        result.Value.Email.ShouldBe(user.Email);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTokenTenantDoesNotOwnTheUser()
    {
        // Arrange
        User user = TestData.NewUser();
        _store.Add(user);
        var handler = new GetCurrentUserQueryHandler(_store, new FakeTenantContext(TenantId.New(), user.Id));

        // Act
        Result<UserResponse> result = await handler.HandleAsync(
            new GetCurrentUserQuery(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(UserErrors.NotFound(user.Id));
    }
}
