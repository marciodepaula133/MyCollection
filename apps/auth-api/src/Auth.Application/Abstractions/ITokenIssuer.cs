using Auth.Domain;

namespace Auth.Application.Abstractions;

/// <summary>Issues a signed access token for a user (AD-6/7).</summary>
public interface ITokenIssuer
{
    /// <summary>
    /// Signs an access token for <paramref name="user"/>. <paramref name="now"/> sets its issue
    /// time; the issuer decides the lifetime and reports the resulting expiry.
    /// </summary>
    AccessToken Issue(User user, DateTimeOffset now);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
