using System.ComponentModel.DataAnnotations;

namespace CleanArchitecture.Infrastructure.Authentication;

internal sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    // HMAC-SHA256 requires a key of at least 256 bits.
    [Required, MinLength(32)]
    public string Secret { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ExpirationInMinutes { get; init; }
}
