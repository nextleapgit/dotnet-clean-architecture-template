using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Users;

namespace CleanArchitecture.UnitTests.Tenants;

public sealed class TenantAccessTests
{
    private readonly UserAdministrationFixture _fixture = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Resolve_Should_ReturnOwnTenant_WhenNoTenantIsRequested()
    {
        Result<TenantId> result = await _fixture.TenantAccessFor(_fixture.Manager).ResolveAsync(null, CancellationToken);

        result.Value.ShouldBe(_fixture.Tenant.Id);
    }

    [Fact]
    public async Task Resolve_Should_ReturnNotFound_WhenManagerRequestsAnotherTenant()
    {
        Guid other = _fixture.OtherTenant.Id.Value;

        Result<TenantId> result = await _fixture.TenantAccessFor(_fixture.Manager).ResolveAsync(other, CancellationToken);

        result.Error.ShouldBe(TenantErrors.NotFound(other));
    }

    [Fact]
    public async Task Resolve_Should_ReturnRequestedTenant_WhenAdminRequestsAnExistingTenant()
    {
        Result<TenantId> result = await _fixture.TenantAccessFor(_fixture.Admin)
            .ResolveAsync(_fixture.OtherTenant.Id.Value, CancellationToken);

        result.Value.ShouldBe(_fixture.OtherTenant.Id);
    }

    [Fact]
    public async Task Resolve_Should_ReturnNotFound_WhenAdminRequestsAnUnknownTenant()
    {
        var unknown = Guid.NewGuid();

        Result<TenantId> result = await _fixture.TenantAccessFor(_fixture.Admin).ResolveAsync(unknown, CancellationToken);

        result.Error.ShouldBe(TenantErrors.NotFound(unknown));
    }

    [Fact]
    public async Task Resolve_Should_ReturnNotFound_WhenAdminIsDeactivated()
    {
        User admin = _fixture.AddUser(_fixture.Platform, Role.Admin);
        admin.Deactivate();

        Result<TenantId> result = await _fixture.TenantAccessFor(admin)
            .ResolveAsync(_fixture.OtherTenant.Id.Value, CancellationToken);

        result.IsFailure.ShouldBeTrue();
    }
}
