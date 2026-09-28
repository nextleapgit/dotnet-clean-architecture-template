using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CleanArchitecture.Infrastructure.Email;

/// <summary>Private keys live outside the database, shared by every instance of this application.</summary>
internal sealed class DataProtectionCertificates : IDisposable
{
    private static readonly object DevelopmentCertificateLock = new();
    private readonly List<X509Certificate2> _certificates = [];

    public DataProtectionCertificates(IConfiguration configuration, IHostEnvironment environment)
    {
        string? path = configuration["DataProtection:CertificatePath"];
        string? password = configuration["DataProtection:CertificatePassword"];
        if (string.IsNullOrWhiteSpace(path))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException("DataProtection:CertificatePath is required outside Development.");
            }

            path = Path.Combine(environment.ContentRootPath, ".containers", "data-protection", "development.pfx");
            EnsureDevelopmentCertificate(path);
        }

        Active = Load(path, password);
        _certificates.Add(Active);
        if (Active.NotAfter.ToUniversalTime() <= DateTime.UtcNow || Active.NotBefore.ToUniversalTime() > DateTime.UtcNow)
        {
            throw new InvalidOperationException("The active Data Protection certificate is outside its validity period.");
        }

        foreach (IConfigurationSection previous in configuration.GetSection("DataProtection:PreviousCertificates").GetChildren())
        {
            _certificates.Add(Load(previous["Path"] ?? throw new InvalidOperationException("Previous certificate path is required."), previous["Password"]));
        }
    }

    public X509Certificate2 Active { get; }

    public X509Certificate2[] All => _certificates.ToArray();

    private static X509Certificate2 Load(string path, string? password)
    {
        X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet);
        using RSA? key = certificate.GetRSAPrivateKey();
        if (key is null || key.KeySize < 2048)
        {
            certificate.Dispose();
            throw new InvalidOperationException("Data Protection requires an RSA certificate with a private key of at least 2048 bits.");
        }
        return certificate;
    }

    private static void EnsureDevelopmentCertificate(string path)
    {
        lock (DevelopmentCertificateLock)
        {
            if (File.Exists(path))
            {
                return;
            }

            string directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            using var rsa = RSA.Create(3072);
            var request = new CertificateRequest("CN=CleanArchitecture Development Data Protection", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(5));
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            stream.Write(certificate.Export(X509ContentType.Pkcs12));
        }
    }

    public void Dispose()
    {
        foreach (X509Certificate2 certificate in _certificates)
        {
            certificate.Dispose();
        }
    }
}
