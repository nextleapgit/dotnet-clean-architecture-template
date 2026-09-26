using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.SharedKernel;
using Microsoft.AspNetCore.DataProtection;

namespace CleanArchitecture.Infrastructure.Email;

/// <summary>
/// Encrypts the email contents. The protection purpose is bound to the logical id and the expiry,
/// so a payload moved to another row or given a longer expiry can no longer be read.
/// </summary>
internal sealed class EmailPayloadProtector(IDataProtectionProvider dataProtectionProvider)
{
    private const string Purpose = "CleanArchitecture.EmailOutbox.Payload.v1";

    public byte[] Protect(EmailMessage message, DateTime expiresAtUtc)
    {
        var payload = new Payload(message.Recipient, message.Subject, message.TextBody, message.HtmlBody);

        return CreateProtector(message.Id, expiresAtUtc).Protect(JsonSerializer.SerializeToUtf8Bytes(payload));
    }

    public Result<EmailMessage> Unprotect(Guid id, DateTime expiresAtUtc, byte[] protectedPayload)
    {
        try
        {
            byte[] bytes = CreateProtector(id, expiresAtUtc).Unprotect(protectedPayload);
            Payload? payload = JsonSerializer.Deserialize<Payload>(bytes);

            return payload is null
                ? Result.Failure<EmailMessage>(ProtectionFailure)
                : EmailMessage.Create(id, payload.Recipient, payload.Subject, payload.TextBody, payload.HtmlBody);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            return Result.Failure<EmailMessage>(ProtectionFailure);
        }
    }

    private static readonly Error ProtectionFailure = Error.Failure(
        EmailErrorCodes.PayloadProtectionFailure,
        "The email payload could not be unprotected.");

    private IDataProtector CreateProtector(Guid id, DateTime expiresAtUtc) =>
        dataProtectionProvider.CreateProtector(
            Purpose,
            id.ToString("N"),
            expiresAtUtc.Ticks.ToString(CultureInfo.InvariantCulture));

    private sealed record Payload(string Recipient, string Subject, string TextBody, string HtmlBody);
}
