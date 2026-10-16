namespace Auth.Api.Endpoints;

/// <summary>
/// The one place the refresh cookie's attributes live (AD-9). Every endpoint that issues,
/// rotates or clears a refresh token goes through here.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "refresh_token";

    public const string Path = "/api/auth";

    public static void Append(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = expiresAt
        });
}
