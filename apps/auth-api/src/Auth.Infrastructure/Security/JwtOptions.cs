using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Auth.Infrastructure.Security;

/// <summary>Bound from the <c>Jwt</c> configuration section (env: <c>Jwt__SigningKeyPath</c>).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// The RS256 private key PEM (AD-6). A relative path is resolved against the content root
    /// before validation.
    /// </summary>
    public string SigningKeyPath { get; set; } = string.Empty;
}

/// <summary>
/// Fails host start (via <c>ValidateOnStart</c>) when the signing key is missing or is not a
/// readable RSA private key, naming the path and the fix. The OpenAPI generator also starts the
/// host, so <c>AddAuthInfrastructure</c> skips <c>ValidateOnStart</c> while it runs (AD-16).
/// </summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKeyPath))
        {
            return ValidateOptionsResult.Fail(
                "Jwt:SigningKeyPath is not set. Point it at the JWT signing key PEM; run "
                + "tools/generate-dev-jwt-key (.sh or .ps1) to create one.");
        }

        if (!File.Exists(options.SigningKeyPath))
        {
            return ValidateOptionsResult.Fail(
                $"JWT signing key not found at '{options.SigningKeyPath}'. "
                + "Run tools/generate-dev-jwt-key (.sh or .ps1) to create one.");
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(options.SigningKeyPath));
            rsa.ExportParameters(includePrivateParameters: true);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException or IOException
            or UnauthorizedAccessException)
        {
            return ValidateOptionsResult.Fail(
                $"JWT signing key at '{options.SigningKeyPath}' is not a readable RSA private key PEM. "
                + "Run tools/generate-dev-jwt-key (.sh or .ps1) to create one.");
        }

        return ValidateOptionsResult.Success;
    }
}
