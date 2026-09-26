using CleanArchitecture.Application.Users.Login;
using CleanArchitecture.Application.Users.Logout;
using CleanArchitecture.Application.Users.Refresh;
using CleanArchitecture.Application.Users.Register;
using CleanArchitecture.Domain.Users;
using FluentValidation.TestHelper;

namespace CleanArchitecture.UnitTests.Users;

public sealed class UserValidatorsTests
{
    private readonly RegisterUserCommandValidator _registerValidator = new();
    private readonly LoginUserCommandValidator _loginValidator = new();
    private readonly RefreshTokenCommandValidator _refreshValidator = new();
    private readonly LogoutUserCommandValidator _logoutValidator = new();

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void RegisterValidator_Should_HaveError_WhenEmailIsInvalid(string email) =>
        _registerValidator.TestValidate(new RegisterUserCommand(email, "Test", "User", "Password123"))
            .ShouldHaveValidationErrorFor(c => c.Email);

    [Fact]
    public void RegisterValidator_Should_HaveErrors_WhenNamesAreEmptyOrTooLong()
    {
        TestValidationResult<RegisterUserCommand> result = _registerValidator.TestValidate(
            new RegisterUserCommand("test@example.com", string.Empty, new string('a', User.NameMaxLength + 1), "Password123"));

        result.ShouldHaveValidationErrorFor(c => c.FirstName);
        result.ShouldHaveValidationErrorFor(c => c.LastName);
    }

    [Fact]
    public void RegisterValidator_Should_HaveError_WhenPasswordIsTooShort() =>
        _registerValidator.TestValidate(new RegisterUserCommand("test@example.com", "Test", "User", "short"))
            .ShouldHaveValidationErrorFor(c => c.Password);

    [Fact]
    public void RegisterValidator_Should_NotHaveErrors_WhenCommandIsValid() =>
        _registerValidator.TestValidate(new RegisterUserCommand("test@example.com", "Test", "User", "Password123"))
            .ShouldNotHaveAnyValidationErrors();

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
}
