using System.Xml.Linq;
using CleanArchitecture.Infrastructure.Database;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CleanArchitecture.Infrastructure.Email;

/// <summary>Wraps legacy plaintext keys without changing key ids or invalidating queued mail.</summary>
internal sealed class DataProtectionKeyEncryption(
    IServiceScopeFactory scopes,
    DataProtectionCertificates certificates,
    ILoggerFactory logging) : IHostedService
{
    private static readonly XNamespace EncryptionNamespace = "http://schemas.asp.net/2015/03/dataProtection";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var encryptor = new CertificateXmlEncryptor(certificates.Active, logging);
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Serializes the upgrade when several application instances start together.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(193640371)", cancellationToken);
        List<DataProtectionKey> keys = await db.DataProtectionKeys.ToListAsync(cancellationToken);
        foreach (DataProtectionKey key in keys)
        {
            if (key.Xml is null)
            {
                continue;
            }
            var xml = XElement.Parse(key.Xml);
            XElement[] secrets = xml.Descendants().Where(element =>
                (string?)element.Attribute(EncryptionNamespace + "requiresEncryption") == "true").ToArray();
            foreach (XElement secret in secrets)
            {
                EncryptedXmlInfo encrypted = encryptor.Encrypt(new XElement(secret));
                secret.ReplaceWith(new XElement(EncryptionNamespace + "encryptedSecret",
                    new XAttribute("decryptorType", encrypted.DecryptorType.AssemblyQualifiedName!), encrypted.EncryptedElement));
            }
            if (secrets.Length > 0)
            {
                key.Xml = xml.ToString(SaveOptions.DisableFormatting);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
