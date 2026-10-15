using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Infrastructure.Security;

/// <summary>
/// The RS256 key pair loaded from <see cref="JwtOptions.SigningKeyPath"/> (AD-6). A singleton,
/// built on first resolve; the <see cref="RSA"/> instance lives as long as the process and is
/// never disposed. <see cref="PublicKey"/> and <see cref="KeyId"/> are exposed for JWKS (CAP-9).
/// </summary>
public sealed class RsaSigningKey
{
    public RsaSigningKey(IOptions<JwtOptions> options)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(options.Value.SigningKeyPath));

        var publicKey = new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: false));

        // RFC 7638 thumbprint of the public key, so the kid is stable for a given key.
        KeyId = Base64UrlEncoder.Encode(
            JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey).ComputeJwkThumbprint());

        publicKey.KeyId = KeyId;
        PublicKey = publicKey;
        SigningKey = new RsaSecurityKey(rsa) { KeyId = KeyId };
    }

    public string KeyId { get; }

    /// <summary>The private key; its <c>KeyId</c> lands in each token's <c>kid</c> header.</summary>
    public RsaSecurityKey SigningKey { get; }

    /// <summary>Public parameters only: safe to publish.</summary>
    public RsaSecurityKey PublicKey { get; }
}
