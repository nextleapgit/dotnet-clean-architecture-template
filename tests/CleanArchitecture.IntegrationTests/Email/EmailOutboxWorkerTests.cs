using CleanArchitecture.BuildingBlocks.Email;
using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.Infrastructure.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.IntegrationTests.Email;

public sealed class EmailOutboxWorkerTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task EnabledWorker_Should_SendQueuedEmails()
    {
        // Arrange
        var sender = new FakeEmailSender(EmailSendResult.Accepted);

        await using WebApplicationFactory<Program> host = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("EmailOutbox:Enabled", "true");
            builder.UseSetting("EmailOutbox:PollIntervalSeconds", "1");
            builder.UseSetting("Smtp:Host", "localhost");
            builder.UseSetting("Smtp:SenderAddress", "no-reply@tests.local");
            builder.ConfigureTestServices(services => services.AddScoped<IEmailSender>(_ => sender));
        });

        EmailMessage message = EmailMessage.Create(
            Guid.CreateVersion7(), "worker@example.com", "Worker test", "Text", "<p>Html</p>").Value;

        // Act
        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(CancellationToken);
            await scope.ServiceProvider.GetRequiredService<IEmailOutbox>()
                .EnqueueAsync(message, DateTime.UtcNow.AddHours(1), CancellationToken);
            await transaction.CommitAsync(CancellationToken);
        }

        EmailOutboxStatus status = EmailOutboxStatus.Pending;
        for (int attempt = 0; attempt < 60 && status != EmailOutboxStatus.Sent; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken);
            status = await WithDbContextAsync(db => db.EmailOutboxMessages.AsNoTracking()
                .Where(m => m.Id == message.Id)
                .Select(m => m.Status)
                .SingleAsync(CancellationToken));
        }

        // Assert
        status.ShouldBe(EmailOutboxStatus.Sent);
        lock (sender.Sent)
        {
            sender.Sent.ShouldContain(message);
        }
    }
}
