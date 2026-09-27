using CleanArchitecture.Domain.Users;
using FluentValidation;

namespace CleanArchitecture.Application.Users;

/// <summary>The rules every use case applies to user fields, so they cannot drift apart.</summary>
internal static class UserValidationRules
{
    public const int PasswordMinLength = 8;

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(User.EmailMaxLength).EmailAddress();

    public static IRuleBuilderOptions<T, string> ValidName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(User.NameMaxLength);

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MinimumLength(PasswordMinLength);

    /// <summary>Admin is never assignable through the API.</summary>
    public static IRuleBuilderOptions<T, Role> AssignableRole<T>(this IRuleBuilder<T, Role> rule) =>
        rule.IsInEnum().NotEqual(Role.Admin);
}
