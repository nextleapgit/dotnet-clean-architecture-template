using CleanArchitecture.Domain.Users;
using FluentValidation;

namespace CleanArchitecture.Application.Users;

/// <summary>The rules every use case applies to user fields, so they cannot drift apart.</summary>
internal static class UserValidationRules
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(User.EmailMaxLength).EmailAddress();

    public static IRuleBuilderOptions<T, string> ValidName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(User.NameMaxLength);

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MinimumLength(PasswordMinLength).MaximumLength(PasswordMaxLength);

    /// <summary>Whether the caller may assign Admin is decided by <see cref="UserManagement"/>.</summary>
    public static IRuleBuilderOptions<T, Role> KnownRole<T>(this IRuleBuilder<T, Role> rule) =>
        rule.IsInEnum();
}
