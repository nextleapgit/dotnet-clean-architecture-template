using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Domain.Users;

public static class UserErrors
{
    public static Error NotFound(Guid userId) => Error.NotFound(
        "Users.NotFound",
        $"The user with the Id = '{userId}' was not found");

    public static readonly Error Forbidden = Error.Forbidden(
        "Users.Forbidden",
        "You are not allowed to perform this action.");

    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Users.InvalidCredentials",
        "The provided email or password is incorrect.");

    public static readonly Error EmailNotUnique = Error.Conflict(
        "Users.EmailNotUnique",
        "The provided email is not unique");

    public static readonly Error InvalidRefreshToken = Error.Problem(
        "Users.InvalidRefreshToken",
        "The provided refresh token is invalid or has expired");
}
