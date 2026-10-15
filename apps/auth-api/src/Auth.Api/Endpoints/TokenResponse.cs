namespace Auth.Api.Endpoints;

/// <summary>
/// The body of every token-issuing response. The refresh token never appears here: it travels
/// only in the HttpOnly <c>refresh_token</c> cookie (AD-9).
/// </summary>
public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
