using CleanArchitecture.Application.Todos.Create;
using CleanArchitecture.Application.Users.Login;
using CleanArchitecture.Application.Users.Passwords;
using CleanArchitecture.Application.Users.Refresh;
using CleanArchitecture.Domain.Todos;
using CleanArchitecture.UnitTests.Fakes;
using FluentValidation.TestHelper;

namespace CleanArchitecture.UnitTests.Users;

public sealed class InputBoundsTests
{
    [Fact]
    public void Authentication_Should_RejectOversizedInputs()
    {
        new LoginUserCommandValidator().TestValidate(new LoginUserCommand("test@example.com", new string('x', 129)))
            .ShouldHaveValidationErrorFor(c => c.Password);
        new ChangePasswordCommandValidator().TestValidate(new ChangePasswordCommand(new string('x', 129), "NewPassword123"))
            .ShouldHaveValidationErrorFor(c => c.CurrentPassword);
        new RefreshTokenCommandValidator().TestValidate(new RefreshTokenCommand(new string('x', 129)))
            .ShouldHaveValidationErrorFor(c => c.RefreshToken);
        new ResetPasswordCommandValidator().TestValidate(new ResetPasswordCommand(new string('x', 129), "NewPassword123"))
            .ShouldHaveValidationErrorFor(c => c.Token);
    }

    [Fact]
    public void Todos_Should_BoundLabelCountAndLength()
    {
        var validator = new CreateTodoCommandValidator(TestData.Clock());
        validator.TestValidate(new CreateTodoCommand("Todo", null, Enumerable.Repeat("label", 21).ToList(), Priority.Low))
            .ShouldHaveValidationErrorFor(c => c.Labels);
        validator.TestValidate(new CreateTodoCommand("Todo", null, [new string('x', 101)], Priority.Low)).IsValid.ShouldBeFalse();
        validator.TestValidate(new CreateTodoCommand("Todo", null, [""], Priority.Low)).IsValid.ShouldBeFalse();
    }
}
