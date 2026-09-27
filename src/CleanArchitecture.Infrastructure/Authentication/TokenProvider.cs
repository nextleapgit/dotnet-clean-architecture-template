using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CleanArchitecture.Infrastructure.Authentication;

internal sealed class TokenProvider(IOptions<JwtOptions> jwtOptions, IDateTimeProvider dateTimeProvider)
    : ITokenProvider
{
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public string CreateAccessToken(User user, Guid sessionId)
    {
        JwtOptions options = jwtOptions.Value;

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(CustomClaimNames.TenantId, user.TenantId.ToString()),
                new Claim(CustomClaimNames.SessionId, sessionId.ToString())
            ]),
            Expires = dateTimeProvider.UtcNow.AddMinutes(options.ExpirationInMinutes),
            SigningCredentials = credentials,
            Issuer = options.Issuer,
            Audience = options.Audience
        };

        return _tokenHandler.CreateToken(tokenDescriptor);
    }

    // URL-safe, so the same tokens can travel in emailed links.
    public string GenerateOpaqueToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    // The token has 256 bits of entropy, so an unsalted SHA-256 is sufficient and allows lookups.
    public string HashOpaqueToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
