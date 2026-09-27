using CleanArchitecture.Application.Abstractions.Paging;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.Get;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.UnitTests.Users;

public sealed class GetUsersQueryHandlerTests
{
    private readonly UserAdministrationFixture _fixture = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private GetUsersQueryHandler HandlerFor(User actor) => new(_fixture.Users, _fixture.TenantAccessFor(actor));

    [Fact]
    public async Task Handle_Should_ReturnOnlyCurrentTenantUsers_WhenOtherTenantsHaveUsers()
    {
        // Arrange
        _fixture.AddUser(_fixture.OtherTenant);

        // Act
        Result<PagedResponse<UserResponse>> result = await HandlerFor(_fixture.Manager).HandleAsync(
            new GetUsersQuery(),
            CancellationToken);

        // Assert
        result.Value.TotalCount.ShouldBe(1);
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(_fixture.Manager.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnRequestedPageOrderedByEmail_WhenUsersSpanSeveralPages()
    {
        // Arrange: the manager is user2@…, so the tenant holds user2, user3, user4.
        _fixture.AddUser(_fixture.Tenant);
        _fixture.AddUser(_fixture.Tenant);

        // Act
        Result<PagedResponse<UserResponse>> result = await HandlerFor(_fixture.Manager).HandleAsync(
            new GetUsersQuery(Page: 2, PageSize: 2),
            CancellationToken);

        // Assert
        result.Value.Items.Select(u => u.Email).ShouldBe(["user4@example.com"]);
        result.Value.TotalCount.ShouldBe(3);
        result.Value.TotalPages.ShouldBe(2);
        result.Value.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ListAnotherTenant_WhenCallerIsAdmin()
    {
        User foreign = _fixture.AddUser(_fixture.OtherTenant);

        Result<PagedResponse<UserResponse>> result = await HandlerFor(_fixture.Admin).HandleAsync(
            new GetUsersQuery(TenantId: _fixture.OtherTenant.Id.Value),
            CancellationToken);

        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(foreign.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenManagerNamesAnotherTenant()
    {
        Guid other = _fixture.OtherTenant.Id.Value;

        Result<PagedResponse<UserResponse>> result = await HandlerFor(_fixture.Manager).HandleAsync(
            new GetUsersQuery(TenantId: other),
            CancellationToken);

        result.Error.ShouldBe(TenantErrors.NotFound(other));
    }
}
