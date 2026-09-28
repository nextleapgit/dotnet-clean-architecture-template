using System.Net.Sockets;
using CleanArchitecture.BuildingBlocks.Email;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CleanArchitecture.Infrastructure.Email;

/// <summary>One SMTP attempt per call. Failures are reduced to fixed, safe error codes.</summary>
internal sealed class SmtpEmailSender(IOptions<SmtpOptions> smtpOptions) : IEmailSender
{
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        SmtpOptions options = smtpOptions.Value;

        using var client = new SmtpClient { Timeout = (int)TimeSpan.FromSeconds(options.TimeoutSeconds).TotalMilliseconds };

        try
        {
            await client.ConnectAsync(options.Host, options.Port, ToSocketOptions(options.SecurityMode), cancellationToken);

            if (!string.IsNullOrEmpty(options.UserName))
            {
                // The options validator guarantees the password accompanies the user name.
                await client.AuthenticateAsync(options.UserName, options.Password!, cancellationToken);
            }

            using MimeMessage mimeMessage = CreateMimeMessage(message, options);

            await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            return EmailSendResult.Accepted;
        }
        catch (SmtpCommandException exception) when ((int)exception.StatusCode >= 500)
        {
            return EmailSendResult.PermanentFailure(EmailErrorCodes.SmtpPermanentFailure);
        }
        catch (Exception exception) when (exception is SmtpCommandException or SmtpProtocolException or AuthenticationException)
        {
            return EmailSendResult.TemporaryFailure(EmailErrorCodes.SmtpTemporaryFailure);
        }
        catch (Exception exception) when (exception is SocketException or IOException or ServiceNotConnectedException)
        {
            return EmailSendResult.TemporaryFailure(EmailErrorCodes.SmtpConnectionFailure);
        }
    }

    private static MimeMessage CreateMimeMessage(EmailMessage message, SmtpOptions options)
    {
        var mimeMessage = new MimeMessage
        {
            // The logical id makes retries of the same email carry the same Message-ID.
            MessageId = $"{message.Id:N}@{MailboxAddress.Parse(options.SenderAddress).Domain}",
            Subject = message.Subject,
            Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody()
        };

        mimeMessage.From.Add(new MailboxAddress(options.SenderName, options.SenderAddress));
        mimeMessage.To.Add(MailboxAddress.Parse(message.Recipient));

        return mimeMessage;
    }

    private static SecureSocketOptions ToSocketOptions(SmtpSecurityMode mode) =>
        mode switch
        {
            SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,
            SmtpSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            SmtpSecurityMode.None => SecureSocketOptions.None,
            _ => throw new InvalidOperationException("Unknown SMTP security mode.")
        };
}
