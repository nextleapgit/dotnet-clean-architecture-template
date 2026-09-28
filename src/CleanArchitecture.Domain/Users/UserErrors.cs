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

    // Reported only after the password was verified, so it does not reveal which emails exist.
    public static readonly Error AccountDisabled = Error.Forbidden(
        "Users.AccountDisabled",
        "The account or its tenant has been deactivated.");

    public static readonly Error LockedOut = Error.Forbidden(
        "Users.LockedOut",
        "Too many failed sign-in attempts. Try again later or reset your password.");

    public static readonly Error InvalidCurrentPassword = Error.Problem(
        "Users.InvalidCurrentPassword",
        "The current password is incorrect.");

    public static readonly Error EmailUnchanged = Error.Problem(
        "Users.EmailUnchanged",
        "The new email address is the current one.");

    public static readonly Error InvalidOrExpiredToken = Error.Problem(
        "Users.InvalidOrExpiredToken",
        "The link is invalid, has already been used, or has expired.");

    public static readonly Error InvitationAlreadyAccepted = Error.Problem(
        "Users.InvitationAlreadyAccepted",
        "The user has already accepted the invitation.");

    public static readonly Error AdminRoleNotAssignable = Error.Problem(
        "Users.AdminRoleNotAssignable",
        "The Admin role can be assigned only by an admin, and only to users of the platform tenant.");

    public static readonly Error AdminNotManageable = Error.Forbidden(
        "Users.AdminNotManageable",
        "Only admins can manage admin accounts.");

    public static readonly Error CannotChangeOwnAccess = Error.Problem(
        "Users.CannotChangeOwnAccess",
        "You cannot change your own role or deactivate your own account.");
}
