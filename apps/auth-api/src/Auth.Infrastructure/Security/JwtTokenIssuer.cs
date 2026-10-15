using Auth.Application.Abstractions;
using Auth.Domain;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Infrastructure.Security;

/// <summary>Signs RS256 access tokens (AD-6/7).</summary>
public sealed class JwtTokenIssuer(RsaSigningKey key) : ITokenIssuer
{
    public const string Issuer = "mycollection-auth";

    public const string Audience = "mycollection";

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private static readonly JsonWebTokenHandler Handler = new();

    public AccessToken Issue(User user, DateTimeOffset now)
    {
        // JWT times are whole seconds; truncate first so the body's expiresAt equals exp.
        now = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds());
        var expiresAt = now + Lifetime;

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString("D"),
            [JwtRegisteredClaimNames.Email] = user.Email,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("D")
        };

        // Password users always have both names (register requires them). The fallback for
        // users without names is decided with OAuth sign-in (CAP-5).
        var name = $"{user.FirstName} {user.LastName}".Trim();
        if (name.Length > 0)
        {
            claims[JwtRegisteredClaimNames.Name] = name;
        }

        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(key.SigningKey, SecurityAlgorithms.RsaSha256)
        });

        return new AccessToken(token, expiresAt);
    }
}
