namespace Auth.Application.Abstractions;

/// <summary>Creates a new opaque refresh token and the hash that is stored for it (AD-8).</summary>
public interface IRefreshTokenGenerator
{
    GeneratedRefreshToken Generate();
}

/// <summary>
/// <paramref name="Raw"/> goes only to the client's cookie; only <paramref name="Hash"/> is stored.
/// </summary>
public sealed record GeneratedRefreshToken(string Raw, string Hash);
