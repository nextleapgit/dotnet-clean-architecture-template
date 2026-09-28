using CleanArchitecture.Application.Tenants.Create;
using CleanArchitecture.Application.Tenants.Get;
using CleanArchitecture.Application.Users.Bootstrap;
using CleanArchitecture.Application.Users.ChangeRole;
using CleanArchitecture.Application.Users.Create;
using CleanArchitecture.Application.Users.EmailChange;
using CleanArchitecture.Application.Users.Get;
using CleanArchitecture.Application.Users.Invitations;
using CleanArchitecture.Application.Users.Passwords;
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
        new(null, "test@example.com", "Test", "User", role);

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
    public void CreateValidator_Should_HaveError_WhenRoleIsUnknown() =>
        _createValidator.TestValidate(ValidCreate((Role)42))
            .ShouldHaveValidationErrorFor(c => c.Role);

    [Theory]
    [InlineData(Role.Member)]
    [InlineData(Role.Manager)]
    [InlineData(Role.Admin)] // allowed by validation; UserManagement decides who may assign it
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
    public void ChangeRoleValidator_Should_HaveError_WhenRoleIsUnknown() =>
        _changeRoleValidator.TestValidate(new ChangeUserRoleCommand(null, Guid.NewGuid(), (Role)42))
            .ShouldHaveValidationErrorFor(c => c.Role);

    [Fact]
    public void PasswordValidators_Should_RejectWeakOrMissingValues()
    {
        TestValidationResult<AcceptInvitationCommand> accept =
            new AcceptInvitationCommandValidator().TestValidate(new AcceptInvitationCommand(string.Empty, "short"));
        accept.ShouldHaveValidationErrorFor(c => c.Token);
        accept.ShouldHaveValidationErrorFor(c => c.Password);
        new ResetPasswordCommandValidator().TestValidate(new ResetPasswordCommand("token", new string('a', 129)))
            .ShouldHaveValidationErrorFor(c => c.NewPassword);
        new ForgotPasswordCommandValidator().TestValidate(new ForgotPasswordCommand("not-an-email"))
            .ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void EmailChangeValidators_Should_RejectMissingOrMalformedValues()
    {
        TestValidationResult<RequestEmailChangeCommand> request =
            new RequestEmailChangeCommandValidator().TestValidate(new RequestEmailChangeCommand(string.Empty, "not-an-email"));
        request.ShouldHaveValidationErrorFor(c => c.CurrentPassword);
        request.ShouldHaveValidationErrorFor(c => c.NewEmail);
        new RequestEmailChangeCommandValidator()
            .TestValidate(new RequestEmailChangeCommand("Password123", $"{new string('a', User.EmailMaxLength)}@example.com"))
            .ShouldHaveValidationErrorFor(c => c.NewEmail);
        new ConfirmEmailChangeCommandValidator().TestValidate(new ConfirmEmailChangeCommand(string.Empty))
            .ShouldHaveValidationErrorFor(c => c.Token);
        new ConfirmEmailChangeCommandValidator().TestValidate(new ConfirmEmailChangeCommand(new string('t', 129)))
            .ShouldHaveValidationErrorFor(c => c.Token);
    }

    [Fact]
    public void EmailChangeValidators_Should_AcceptValidValues()
    {
        new RequestEmailChangeCommandValidator().TestValidate(new RequestEmailChangeCommand("Password123", "new@example.com"))
            .ShouldNotHaveAnyValidationErrors();
        new ConfirmEmailChangeCommandValidator().TestValidate(new ConfirmEmailChangeCommand("token"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ChangePasswordValidator_Should_RejectReusingTheCurrentPassword() =>
        new ChangePasswordCommandValidator().TestValidate(new ChangePasswordCommand("Password123", "Password123"))
            .ShouldHaveValidationErrorFor(c => c.NewPassword);

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
