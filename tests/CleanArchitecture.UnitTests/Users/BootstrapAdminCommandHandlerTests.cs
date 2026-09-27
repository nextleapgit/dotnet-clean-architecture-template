using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Application.Users.Bootstrap;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using CleanArchitecture.UnitTests.Fakes;

namespace CleanArchitecture.UnitTests.Users;

public sealed class BootstrapAdminCommandHandlerTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InMemoryTenantStore _tenantStore = new();
    private readonly InMemoryUserStore _userStore = new();
    private readonly RecordingAuditLog _auditLog = new();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();

    private static readonly BootstrapAdminCommand Command =
        new("Platform", "Admin@Example.com", "Platform", "Admin", "Password123");

    private BootstrapAdminCommandHandler Handler =>
        new(_unitOfWork, _tenantStore, _userStore, _passwordHasher, TestData.Clock(), _auditLog);

    [Fact]
    public async Task Handle_Should_CreatePlatformTenantAndAdmin_WhenNoAdminExists()
    {
        Result result = await Handler.HandleAsync(Command, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        Tenant tenant = _tenantStore.Tenants.ShouldHaveSingleItem();
        tenant.Name.ShouldBe("Platform");
        tenant.IsPlatform.ShouldBeTrue();
        User admin = _userStore.Users.ShouldHaveSingleItem();
        admin.Role.ShouldBe(Role.Admin);
        admin.Email.ShouldBe("admin@example.com");
        admin.TenantId.ShouldBe(tenant.Id);
        _auditLog.Records.ShouldHaveSingleItem().Action.ShouldBe(UserAuditActions.AdminBootstrapped);
        _unitOfWork.SaveChangesCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_DoNothing_WhenAnAdminAlreadyExists()
    {
        _userStore.Add(TestData.NewUser(email: "existing-admin@example.com", role: Role.Admin));

        Result result = await Handler.HandleAsync(Command, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _tenantStore.Tenants.ShouldBeEmpty();
        _userStore.Users.Count.ShouldBe(1);
        _unitOfWork.SaveChangesCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenEmailBelongsToAnExistingUser()
    {
        _userStore.Add(TestData.NewUser(email: "admin@example.com"));

        Result result = await Handler.HandleAsync(Command, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(UserErrors.EmailNotUnique);
        _unitOfWork.SaveChangesCount.ShouldBe(0);
    }
}
