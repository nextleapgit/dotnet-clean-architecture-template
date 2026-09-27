using CleanArchitecture.Application.Abstractions.Paging;
using CleanArchitecture.Application.Tenants;
using CleanArchitecture.Application.Tenants.Activate;
using CleanArchitecture.Application.Tenants.Create;
using CleanArchitecture.Application.Tenants.Deactivate;
using CleanArchitecture.Application.Tenants.Get;
using CleanArchitecture.Application.Tenants.GetById;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;
using CleanArchitecture.UnitTests.Users;

namespace CleanArchitecture.UnitTests.Tenants;

public sealed class TenantCommandHandlerTests
{
    private readonly UserAdministrationFixture _fixture = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private CreateTenantCommandHandler CreateHandler(User actor) =>
        new(_fixture.UnitOfWork, _fixture.TenantAccessFor(actor), _fixture.Tenants, TestData.Clock(), _fixture.AuditLog);

    private DeactivateTenantCommandHandler DeactivateHandler(User actor) =>
        new(_fixture.UnitOfWork, _fixture.TenantAccessFor(actor), _fixture.Tenants, _fixture.ContextOf(actor), _fixture.AuditLog);

    private ActivateTenantCommandHandler ActivateHandler(User actor) =>
        new(_fixture.UnitOfWork, _fixture.TenantAccessFor(actor), _fixture.Tenants, _fixture.AuditLog);

    [Fact]
    public async Task Create_Should_AddTenantAndAudit_WhenCallerIsAdmin()
    {
        Result<Guid> result = await CreateHandler(_fixture.Admin).HandleAsync(new CreateTenantCommand("Initech"), CancellationToken);

        Tenant tenant = _fixture.Tenants.Tenants.Single(t => t.Id.Value == result.Value);
        tenant.Name.ShouldBe("Initech");
        tenant.IsActive.ShouldBeTrue();
        _fixture.AuditLog.Records.ShouldHaveSingleItem().Action.ShouldBe(TenantAuditActions.Created);
        _fixture.UnitOfWork.SaveChangesCount.ShouldBe(1);
    }

    [Fact]
    public async Task Create_Should_ReturnForbidden_WhenCallerIsNotAdmin()
    {
        int before = _fixture.Tenants.Tenants.Count;

        Result<Guid> result = await CreateHandler(_fixture.Manager).HandleAsync(new CreateTenantCommand("Initech"), CancellationToken);

        result.Error.ShouldBe(UserErrors.Forbidden);
        _fixture.Tenants.Tenants.Count.ShouldBe(before);
    }

    [Fact]
    public async Task Deactivate_Should_DeactivateAndAudit_WhenCallerIsAdmin()
    {
        Result result = await DeactivateHandler(_fixture.Admin)
            .HandleAsync(new DeactivateTenantCommand(_fixture.Tenant.Id.Value), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _fixture.Tenant.IsActive.ShouldBeFalse();
        AuditRecord audit = _fixture.AuditLog.Records.ShouldHaveSingleItem();
        audit.Action.ShouldBe(TenantAuditActions.Deactivated);
        audit.Severity.ShouldBe(AuditSeverity.Warning);
    }

    [Fact]
    public async Task Deactivate_Should_Fail_WhenAdminTargetsOwnTenant()
    {
        Result result = await DeactivateHandler(_fixture.Admin)
            .HandleAsync(new DeactivateTenantCommand(_fixture.Platform.Id.Value), CancellationToken);

        result.Error.ShouldBe(TenantErrors.CannotDeactivateOwnTenant);
        _fixture.Platform.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Deactivate_Should_ReturnForbidden_WhenCallerIsNotAdmin()
    {
        Result result = await DeactivateHandler(_fixture.Manager)
            .HandleAsync(new DeactivateTenantCommand(_fixture.OtherTenant.Id.Value), CancellationToken);

        result.Error.ShouldBe(UserErrors.Forbidden);
        _fixture.OtherTenant.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Deactivate_Should_ReturnNotFound_WhenTenantDoesNotExist()
    {
        var unknown = Guid.NewGuid();

        Result result = await DeactivateHandler(_fixture.Admin).HandleAsync(new DeactivateTenantCommand(unknown), CancellationToken);

        result.Error.ShouldBe(TenantErrors.NotFound(unknown));
    }

    [Fact]
    public async Task Activate_Should_ReactivateAndAudit_WhenTenantIsInactive()
    {
        _fixture.OtherTenant.Deactivate();

        Result result = await ActivateHandler(_fixture.Admin)
            .HandleAsync(new ActivateTenantCommand(_fixture.OtherTenant.Id.Value), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _fixture.OtherTenant.IsActive.ShouldBeTrue();
        _fixture.AuditLog.Records.ShouldHaveSingleItem().Action.ShouldBe(TenantAuditActions.Activated);
    }

    [Fact]
    public async Task GetTenants_Should_ListAllTenants_WhenCallerIsAdmin()
    {
        var handler = new GetTenantsQueryHandler(_fixture.Tenants, _fixture.TenantAccessFor(_fixture.Admin));

        Result<PagedResponse<TenantResponse>> result = await handler.HandleAsync(new GetTenantsQuery(), CancellationToken);

        result.Value.TotalCount.ShouldBe(3);
        result.Value.Items.Select(t => t.Name).ShouldBe(["Acme", "Globex", "Platform"]);
    }

    [Fact]
    public async Task GetTenants_Should_ReturnForbidden_WhenCallerIsNotAdmin()
    {
        var handler = new GetTenantsQueryHandler(_fixture.Tenants, _fixture.TenantAccessFor(_fixture.Manager));

        Result<PagedResponse<TenantResponse>> result = await handler.HandleAsync(new GetTenantsQuery(), CancellationToken);

        result.Error.ShouldBe(UserErrors.Forbidden);
    }

    [Fact]
    public async Task GetTenantById_Should_ReturnNotFound_WhenManagerAsksForAnotherTenant()
    {
        var handler = new GetTenantByIdQueryHandler(_fixture.Tenants, _fixture.TenantAccessFor(_fixture.Manager));
        Guid other = _fixture.OtherTenant.Id.Value;

        Result<TenantResponse> result = await handler.HandleAsync(new GetTenantByIdQuery(other), CancellationToken);

        result.Error.ShouldBe(TenantErrors.NotFound(other));
    }
}
