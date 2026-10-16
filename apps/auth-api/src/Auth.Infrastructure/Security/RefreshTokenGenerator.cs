using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Auth.Application.Abstractions;

namespace Auth.Infrastructure.Security;

/// <summary>
/// AD-8: 256 bits from a CSPRNG, base64url-encoded; only the SHA-256 hex of that string is stored.
/// </summary>
public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    private const int TokenBytes = 32;

    public GeneratedRefreshToken Generate()
    {
        var raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        return new GeneratedRefreshToken(raw, ComputeHash(raw));
    }

    /// <summary>Lower-case SHA-256 hex of the raw token's ASCII bytes, as stored in <c>token_hash</c>.</summary>
    public static string ComputeHash(string rawToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(rawToken)));
}
