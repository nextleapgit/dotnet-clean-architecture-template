using System.Globalization;
using System.Net;
using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users;

/// <summary>Account emails. Names and links are HTML-encoded before they reach an HTML body.</summary>
internal static class UserEmails
{
    public static Result<EmailMessage> Invitation(User user, string link, DateTime expiresAtUtc) =>
        Create(
            user.Email,
            "You have been invited",
            user.FirstName,
            "An account has been created for you. Choose your password to activate it:",
            link,
            $"The link expires on {Format(expiresAtUtc)} UTC.");

    public static Result<EmailMessage> PasswordReset(User user, string link, DateTime expiresAtUtc) =>
        Create(
            user.Email,
            "Reset your password",
            user.FirstName,
            "We received a request to reset your password. Choose a new one here:",
            link,
            $"The link expires on {Format(expiresAtUtc)} UTC. If you did not ask for this, ignore this email.");

    public static Result<EmailMessage> PasswordChanged(User user) =>
        Create(
            user.Email,
            "Your password was changed",
            user.FirstName,
            "The password of your account was just changed, and your other sessions were signed out.",
            link: null,
            "If this was not you, reset your password immediately and contact your administrator.");

    private static Result<EmailMessage> Create(
        string recipient,
        string subject,
        string firstName,
        string intro,
        string? link,
        string outro)
    {
        string text = link is null
            ? $"Hello {firstName},\r\n\r\n{intro}\r\n\r\n{outro}"
            : $"Hello {firstName},\r\n\r\n{intro}\r\n{link}\r\n\r\n{outro}";

        string linkHtml = link is null
            ? string.Empty
            : $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(link)}</a></p>";

        string html =
            $"<p>Hello {WebUtility.HtmlEncode(firstName)},</p><p>{WebUtility.HtmlEncode(intro)}</p>{linkHtml}<p>{WebUtility.HtmlEncode(outro)}</p>";

        return EmailMessage.Create(Guid.CreateVersion7(), recipient, subject, text, html);
    }

    private static string Format(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
