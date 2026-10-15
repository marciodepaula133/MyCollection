namespace Auth.Application.Features.Login;

public sealed record LoginCommand(string Email, string Password);

/// <summary>
/// <paramref name="RefreshToken"/> is the raw token; the Api writes it only to the
/// HttpOnly cookie, never to the body.
/// </summary>
public sealed record LoginResult(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt);
