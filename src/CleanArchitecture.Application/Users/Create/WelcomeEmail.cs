using System.Net;
using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Create;

internal static class WelcomeEmail
{
    public static Result<EmailMessage> Create(User user)
    {
        string text = $"Hello {user.FirstName},\r\n\r\nYour account is ready.";

        // Names are user input: encode them before they reach the HTML body.
        string html = $"<p>Hello {WebUtility.HtmlEncode(user.FirstName)},</p><p>Your account is ready.</p>";

        return EmailMessage.Create(Guid.CreateVersion7(), user.Email, "Welcome", text, html);
    }
}
