using CleanArchitecture.Application.Abstractions.Paging;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.Get;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Users;

public sealed class GetUsersQueryHandlerTests
{
    private readonly InMemoryUserStore _store = new();
    private readonly TenantId _tenantId = TenantId.New();

    private GetUsersQueryHandler CreateHandler() =>
        new(_store, new FakeTenantContext(_tenantId, Guid.NewGuid()));

    [Fact]
    public async Task Handle_Should_ReturnOnlyCurrentTenantUsers_WhenOtherTenantsHaveUsers()
    {
        // Arrange
        User own = TestData.NewUser(_tenantId, "own@example.com");
        _store.Add(own);
        _store.Add(TestData.NewUser(TenantId.New(), "foreign@example.com"));

        // Act
        Result<PagedResponse<UserResponse>> result = await CreateHandler().HandleAsync(
            new GetUsersQuery(),
            TestContext.Current.CancellationToken);

        // Assert
        result.Value.TotalCount.ShouldBe(1);
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(own.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnRequestedPageOrderedByEmail_WhenUsersSpanSeveralPages()
    {
        // Arrange
        _store.Add(TestData.NewUser(_tenantId, "c@example.com"));
        _store.Add(TestData.NewUser(_tenantId, "a@example.com"));
        _store.Add(TestData.NewUser(_tenantId, "b@example.com"));

        // Act
        Result<PagedResponse<UserResponse>> result = await CreateHandler().HandleAsync(
            new GetUsersQuery(Page: 2, PageSize: 2),
            TestContext.Current.CancellationToken);

        // Assert
        result.Value.Items.Select(u => u.Email).ShouldBe(["c@example.com"]);
        result.Value.TotalCount.ShouldBe(3);
        result.Value.TotalPages.ShouldBe(2);
        result.Value.HasNextPage.ShouldBeFalse();
    }
}
