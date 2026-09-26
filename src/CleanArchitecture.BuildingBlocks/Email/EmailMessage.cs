using System.Net.Mail;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.BuildingBlocks.Email;

/// <summary>
/// An immutable email. <see cref="Id"/> is the stable logical id reused across retries (it becomes
/// the Message-ID header). Bodies are sent as given: callers must HTML-encode untrusted values.
/// </summary>
public sealed record EmailMessage
{
    public const int MaxRecipientLength = 254;
    public const int MaxSubjectLength = 255;
    public const int MaxCombinedBodyLength = 256 * 1024;

    private EmailMessage(Guid id, string recipient, string subject, string textBody, string htmlBody)
    {
        Id = id;
        Recipient = recipient;
        Subject = subject;
        TextBody = textBody;
        HtmlBody = htmlBody;
    }

    public Guid Id { get; }
    public string Recipient { get; }
    public string Subject { get; }
    public string TextBody { get; }
    public string HtmlBody { get; }

    public static Result<EmailMessage> Create(
        Guid id,
        string recipient,
        string subject,
        string textBody,
        string htmlBody)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<EmailMessage>(EmailErrors.InvalidId);
        }

        if (string.IsNullOrWhiteSpace(recipient) ||
            recipient.Length > MaxRecipientLength ||
            recipient.Any(char.IsControl) ||
            !MailAddress.TryCreate(recipient, out MailAddress? address) ||
            address.Address != recipient)
        {
            return Result.Failure<EmailMessage>(EmailErrors.InvalidRecipient);
        }

        // Control characters in headers would allow header injection.
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > MaxSubjectLength || subject.Any(char.IsControl))
        {
            return Result.Failure<EmailMessage>(EmailErrors.InvalidSubject);
        }

        if (string.IsNullOrWhiteSpace(textBody) ||
            string.IsNullOrWhiteSpace(htmlBody) ||
            textBody.Length + htmlBody.Length > MaxCombinedBodyLength ||
            HasDisallowedControlCharacters(textBody) ||
            HasDisallowedControlCharacters(htmlBody))
        {
            return Result.Failure<EmailMessage>(EmailErrors.InvalidBody);
        }

        return new EmailMessage(id, recipient, subject, textBody, htmlBody);
    }

    private static bool HasDisallowedControlCharacters(string value) =>
        value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));
}
