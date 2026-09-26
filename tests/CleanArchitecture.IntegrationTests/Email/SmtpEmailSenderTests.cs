using System.Text.Json;
using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.Infrastructure.Email;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.IntegrationTests.Email;

/// <summary>Real SMTP round trips against a Mailpit container.</summary>
public sealed class SmtpEmailSenderTests : IAsyncLifetime
{
    private const int SmtpPort = 1025;
    private const int ApiPort = 8025;

    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.31")
        .WithPortBinding(SmtpPort, assignRandomHostPort: true)
        .WithPortBinding(ApiPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
            request.ForPort(ApiPort).ForPath("/livez")))
        .Build();

    public async ValueTask InitializeAsync() => await _mailpit.StartAsync();

    public async ValueTask DisposeAsync() => await _mailpit.DisposeAsync();

    private SmtpEmailSender CreateSender(int? port = null) =>
        new(Options.Create(new SmtpOptions
        {
            Host = _mailpit.Hostname,
            Port = port ?? _mailpit.GetMappedPublicPort(SmtpPort),
            SecurityMode = SmtpSecurityMode.None,
            SenderAddress = "no-reply@tests.local",
            SenderName = "Tests",
            TimeoutSeconds = 10
        }));

    [Fact]
    public async Task Send_Should_DeliverMessageWithStableMessageId()
    {
        // Arrange
        EmailMessage message = EmailMessage.Create(
            Guid.CreateVersion7(), "someone@example.com", "مرحباً — Welcome", "Text", "<p>Html</p>").Value;

        // Act
        EmailSendResult result = await CreateSender().SendAsync(message, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(EmailSendResult.Accepted);

        using var http = new HttpClient
        {
            BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(ApiPort)}")
        };
        using var inbox = JsonDocument.Parse(
            await http.GetStringAsync("/api/v1/messages", TestContext.Current.CancellationToken));
        JsonElement stored = inbox.RootElement.GetProperty("messages").EnumerateArray().Single();

        stored.GetProperty("Subject").GetString().ShouldBe("مرحباً — Welcome");
        stored.GetProperty("MessageID").GetString()!.ShouldContain(message.Id.ToString("N"));
    }

    [Fact]
    public async Task Send_Should_ReportAConnectionFailure_WhenServerIsUnreachable()
    {
        EmailMessage message = EmailMessage.Create(Guid.CreateVersion7(), "someone@example.com", "Subject", "Text", "<p>Html</p>").Value;

        EmailSendResult result = await CreateSender(port: 1).SendAsync(message, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(EmailSendOutcome.TemporaryFailure);
        result.ErrorCode.ShouldBe(EmailErrorCodes.SmtpConnectionFailure);
    }
}
