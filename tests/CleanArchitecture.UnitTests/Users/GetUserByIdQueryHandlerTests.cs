using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.GetById;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.UnitTests.Users;

public sealed class GetUserByIdQueryHandlerTests
{
    private readonly UserAdministrationFixture _fixture = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private GetUserByIdQueryHandler HandlerFor(User actor) => new(_fixture.Users, _fixture.TenantAccessFor(actor));

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserBelongsToAnotherTenant()
    {
        // Arrange
        User foreign = _fixture.AddUser(_fixture.OtherTenant);

        // Act
        Result<UserResponse> result = await HandlerFor(_fixture.Manager).HandleAsync(
            new GetUserByIdQuery(foreign.Id),
            CancellationToken);

        // Assert
        result.Error.ShouldBe(UserErrors.NotFound(foreign.Id));
    }

    [Fact]
    public async Task Handle_Should_ReturnUser_WhenInSameTenant()
    {
        // Arrange
        User user = _fixture.AddUser(_fixture.Tenant);

        // Act
        Result<UserResponse> result = await HandlerFor(_fixture.Manager).HandleAsync(
            new GetUserByIdQuery(user.Id),
            CancellationToken);

        // Assert
        result.Value.Id.ShouldBe(user.Id);
        result.Value.TenantId.ShouldBe(user.TenantId.Value);
        result.Value.Role.ShouldBe(Role.Member);
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnUserOfAnotherTenant_WhenCallerIsAdmin()
    {
        // Arrange
        User foreign = _fixture.AddUser(_fixture.OtherTenant);

        // Act
        Result<UserResponse> result = await HandlerFor(_fixture.Admin).HandleAsync(
            new GetUserByIdQuery(foreign.Id, _fixture.OtherTenant.Id.Value),
            CancellationToken);

        // Assert
        result.Value.Id.ShouldBe(foreign.Id);
    }
}
