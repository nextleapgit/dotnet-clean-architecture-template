using CleanArchitecture.Application.Tenants.Create;
using CleanArchitecture.Application.Tenants.Get;
using CleanArchitecture.Application.Users.Bootstrap;
using CleanArchitecture.Application.Users.ChangeRole;
using CleanArchitecture.Application.Users.Create;
using CleanArchitecture.Application.Users.Get;
using CleanArchitecture.Application.Users.Login;
using CleanArchitecture.Application.Users.Logout;
using CleanArchitecture.Application.Users.Refresh;
using CleanArchitecture.Application.Users.Update;
using CleanArchitecture.Domain.Tenants;
using CleanArchitecture.Domain.Users;
using FluentValidation.TestHelper;

namespace CleanArchitecture.UnitTests.Users;

public sealed class UserValidatorsTests
{
    private readonly CreateUserCommandValidator _createValidator = new();
    private readonly UpdateUserCommandValidator _updateValidator = new();
    private readonly ChangeUserRoleCommandValidator _changeRoleValidator = new();
    private readonly BootstrapAdminCommandValidator _bootstrapValidator = new();
    private readonly CreateTenantCommandValidator _createTenantValidator = new();
    private readonly LoginUserCommandValidator _loginValidator = new();
    private readonly RefreshTokenCommandValidator _refreshValidator = new();
    private readonly LogoutUserCommandValidator _logoutValidator = new();
    private readonly GetUsersQueryValidator _getUsersValidator = new();
    private readonly GetTenantsQueryValidator _getTenantsValidator = new();

    private static CreateUserCommand ValidCreate(Role role = Role.Member) =>
        new(null, "test@example.com", "Test", "User", "Password123", role);

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void CreateValidator_Should_HaveError_WhenEmailIsInvalid(string email) =>
        _createValidator.TestValidate(ValidCreate() with { Email = email })
            .ShouldHaveValidationErrorFor(c => c.Email);

    [Fact]
    public void CreateValidator_Should_HaveErrors_WhenNamesAreEmptyOrTooLong()
    {
        TestValidationResult<CreateUserCommand> result = _createValidator.TestValidate(
            ValidCreate() with { FirstName = string.Empty, LastName = new string('a', User.NameMaxLength + 1) });

        result.ShouldHaveValidationErrorFor(c => c.FirstName);
        result.ShouldHaveValidationErrorFor(c => c.LastName);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenPasswordIsTooShort() =>
        _createValidator.TestValidate(ValidCreate() with { Password = "short" })
            .ShouldHaveValidationErrorFor(c => c.Password);

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData((Role)42)]
    public void CreateValidator_Should_HaveError_WhenRoleIsNotAssignable(Role role) =>
        _createValidator.TestValidate(ValidCreate(role))
            .ShouldHaveValidationErrorFor(c => c.Role);

    [Theory]
    [InlineData(Role.Member)]
    [InlineData(Role.Manager)]
    public void CreateValidator_Should_NotHaveErrors_WhenCommandIsValid(Role role) =>
        _createValidator.TestValidate(ValidCreate(role)).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void UpdateValidator_Should_HaveErrors_WhenFieldsAreEmpty()
    {
        TestValidationResult<UpdateUserCommand> result = _updateValidator.TestValidate(
            new UpdateUserCommand(null, Guid.Empty, string.Empty, string.Empty));

        result.ShouldHaveValidationErrorFor(c => c.UserId);
        result.ShouldHaveValidationErrorFor(c => c.FirstName);
        result.ShouldHaveValidationErrorFor(c => c.LastName);
    }

    [Fact]
    public void ChangeRoleValidator_Should_HaveError_WhenRoleIsAdmin() =>
        _changeRoleValidator.TestValidate(new ChangeUserRoleCommand(null, Guid.NewGuid(), Role.Admin))
            .ShouldHaveValidationErrorFor(c => c.Role);

    [Fact]
    public void BootstrapValidator_Should_HaveErrors_WhenFieldsAreMissing()
    {
        TestValidationResult<BootstrapAdminCommand> result = _bootstrapValidator.TestValidate(
            new BootstrapAdminCommand(string.Empty, "not-an-email", "A", "B", "short"));

        result.ShouldHaveValidationErrorFor(c => c.TenantName);
        result.ShouldHaveValidationErrorFor(c => c.Email);
        result.ShouldHaveValidationErrorFor(c => c.Password);
    }

    [Fact]
    public void CreateTenantValidator_Should_HaveError_WhenNameIsEmptyOrTooLong()
    {
        _createTenantValidator.TestValidate(new CreateTenantCommand(string.Empty))
            .ShouldHaveValidationErrorFor(c => c.Name);
        _createTenantValidator.TestValidate(new CreateTenantCommand(new string('a', Tenant.NameMaxLength + 1)))
            .ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void LoginValidator_Should_HaveErrors_WhenFieldsAreEmpty()
    {
        TestValidationResult<LoginUserCommand> result = _loginValidator.TestValidate(
            new LoginUserCommand(string.Empty, string.Empty));

        result.ShouldHaveValidationErrorFor(c => c.Email);
        result.ShouldHaveValidationErrorFor(c => c.Password);
    }

    [Fact]
    public void RefreshAndLogoutValidators_Should_HaveError_WhenTokenIsEmpty()
    {
        _refreshValidator.TestValidate(new RefreshTokenCommand(string.Empty))
            .ShouldHaveValidationErrorFor(c => c.RefreshToken);
        _logoutValidator.TestValidate(new LogoutUserCommand(string.Empty))
            .ShouldHaveValidationErrorFor(c => c.RefreshToken);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, GetUsersQuery.MaxPageSize + 1)]
    public void PagingValidators_Should_HaveError_WhenPagingIsOutOfRange(int page, int pageSize)
    {
        _getUsersValidator.TestValidate(new GetUsersQuery(page, pageSize)).IsValid.ShouldBeFalse();
        _getTenantsValidator.TestValidate(new GetTenantsQuery(page, pageSize)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void PagingValidators_Should_NotHaveErrors_WhenPagingIsValid()
    {
        _getUsersValidator.TestValidate(new GetUsersQuery(1, GetUsersQuery.MaxPageSize)).ShouldNotHaveAnyValidationErrors();
        _getTenantsValidator.TestValidate(new GetTenantsQuery(1, GetTenantsQuery.MaxPageSize)).ShouldNotHaveAnyValidationErrors();
    }
}
