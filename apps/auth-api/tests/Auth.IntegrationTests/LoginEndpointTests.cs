using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Auth.Domain;
using Auth.Infrastructure;
using Auth.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Auth.IntegrationTests;

public class LoginEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private const string ValidPassword = "s3cret-pass";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Cookie handling off: the tests read the raw Set-Cookie header.
    private HttpClient Client => fixture.Migrated.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    [Fact]
    public async Task Valid_login_sets_the_refresh_cookie_and_stores_only_its_hash()
    {
        var (email, userId) = await RegisterUser();

        var response = await Login($"  {email.ToUpperInvariant()} ", ValidPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("accessToken").GetString()));
        Assert.True(body.RootElement.TryGetProperty("expiresAt", out _));
        Assert.Equal(2, body.RootElement.EnumerateObject().Count());

        var cookie = AssertRefreshCookie(response);
        Assert.DoesNotContain(cookie.Value.ToString(), await response.Content.ReadAsStringAsync(Ct));

        var row = Assert.Single(await RefreshTokensOf(userId));
        Assert.Equal(RefreshTokenGenerator.ComputeHash(cookie.Value.ToString()), row.TokenHash);
        Assert.NotEqual(cookie.Value.ToString(), row.TokenHash);
        Assert.Equal(row.ExpiresAt.AddDays(60), row.FamilyExpiresAt);
        Assert.Null(row.ReplacedById);
        Assert.Null(row.ConsumedAt);
        Assert.Null(row.RevokedAt);

        // Set-Cookie carries whole seconds.
        Assert.Equal(row.ExpiresAt.ToUnixTimeSeconds(), cookie.Expires!.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Access_token_validates_with_the_public_key_and_carries_the_claims()
    {
        var (email, userId) = await RegisterUser();
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await using var factory = fixture.Migrated.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FixedTimeProvider(now))));

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email, password = ValidPassword }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(now.AddMinutes(15), body.RootElement.GetProperty("expiresAt").GetDateTimeOffset());

        var publicKey = new RsaSecurityKey(fixture.SigningPublicKey);
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(
            body.RootElement.GetProperty("accessToken").GetString(),
            new TokenValidationParameters
            {
                ValidIssuer = "mycollection-auth",
                ValidAudience = "mycollection",
                IssuerSigningKey = publicKey,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
            });
        Assert.True(validation.IsValid, validation.Exception?.Message);

        var token = (JsonWebToken)validation.SecurityToken;
        var expectedKid = Base64UrlEncoder.Encode(
            JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey).ComputeJwkThumbprint());
        Assert.Equal(expectedKid, token.Kid);
        Assert.Equal(SecurityAlgorithms.RsaSha256, token.Alg);
        Assert.Equal(userId.ToString("D"), token.Subject);
        Assert.Equal(email, token.GetClaim("email").Value);
        Assert.Equal("Test User", token.GetClaim("name").Value);
        Assert.True(Guid.TryParse(token.Id, out _));
        Assert.Equal(now.ToUnixTimeSeconds(), token.GetPayloadValue<long>("iat"));
        Assert.Equal(now.ToUnixTimeSeconds(), token.GetPayloadValue<long>("nbf"));
        Assert.Equal(now.AddMinutes(15).ToUnixTimeSeconds(), token.GetPayloadValue<long>("exp"));
    }

    [Fact]
    public async Task Second_login_starts_a_new_family_and_leaves_the_first_untouched()
    {
        var (email, userId) = await RegisterUser();

        Assert.Equal(HttpStatusCode.OK, (await Login(email, ValidPassword)).StatusCode);
        var first = Assert.Single(await RefreshTokensOf(userId));
        Assert.Equal(HttpStatusCode.OK, (await Login(email, ValidPassword)).StatusCode);

        var rows = await RefreshTokensOf(userId);
        Assert.Equal(2, rows.Count);
        var second = Assert.Single(rows, r => r.Id != first.Id);
        Assert.NotEqual(first.FamilyId, second.FamilyId);
        Assert.NotEqual(first.TokenHash, second.TokenHash);

        var firstAgain = Assert.Single(rows, r => r.Id == first.Id);
        Assert.Equal(first.ExpiresAt, firstAgain.ExpiresAt);
        Assert.Null(firstAgain.ReplacedById);
        Assert.Null(firstAgain.ConsumedAt);
        Assert.Null(firstAgain.RevokedAt);
    }

    [Fact]
    public async Task Every_credential_failure_is_the_same_401_invalid_credentials()
    {
        var (email, userId) = await RegisterUser();
        var withoutPassword = await AddUserWithoutPassword();

        var wrongPassword = await Login(email, "wrong-pass");
        var unknownEmail = await Login($"nobody-{Guid.NewGuid():N}@example.com", ValidPassword);
        var noCredential = await Login(withoutPassword.Email, ValidPassword);

        var bodies = new List<JsonElement>();
        foreach (var response in new[] { wrongPassword, unknownEmail, noCredential })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.False(response.Headers.Contains(HeaderNames.SetCookie));
            await AssertProblem(response, "invalid_credentials");
            bodies.Add(JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement);
        }

        // Identical apart from the per-request traceId.
        foreach (var property in new[] { "type", "title", "status", "code" })
        {
            Assert.Single(bodies.Select(b => b.GetProperty(property).ToString()).Distinct());
        }

        Assert.Empty(await RefreshTokensOf(userId));
        Assert.Empty(await RefreshTokensOf(withoutPassword.Id));
    }

    [Theory]
    [InlineData("""{"password":"s3cret-pass"}""")]
    [InlineData("""{"email":"not-an-email","password":"s3cret-pass"}""")]
    [InlineData("""{"email":"","password":"s3cret-pass"}""")]
    [InlineData("""{"email":"a@b.com"}""")]
    [InlineData("""{"email":"a@b.com","password":""}""")]
    public async Task Invalid_fields_are_a_400_validation_failed(string json)
    {
        var response = await PostJson(json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblem(response, "validation_failed");
    }

    [Theory]
    [InlineData(1024, HttpStatusCode.Unauthorized)]
    [InlineData(1025, HttpStatusCode.BadRequest)]
    public async Task Password_length_cap_is_1024(int length, HttpStatusCode expected)
    {
        // A registered user, so a 1024-character password really reaches bcrypt.
        var (email, _) = await RegisterUser();

        var response = await Login(email, new string('a', length));

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("")]
    public async Task Malformed_body_is_a_400_malformed_request(string json)
    {
        var response = await PostJson(json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblem(response, "malformed_request");
    }

    [Fact]
    public async Task Wrong_content_type_is_a_415_unsupported_media_type()
    {
        using var content = new StringContent("email=a@b.com&password=s3cret-pass", Encoding.UTF8, "text/plain");

        var response = await Client.PostAsync("/api/auth/login", content, Ct);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        await AssertProblem(response, "unsupported_media_type");
    }

    private static SetCookieHeaderValue AssertRefreshCookie(HttpResponseMessage response)
    {
        var header = Assert.Single(response.Headers.GetValues(HeaderNames.SetCookie));
        var cookie = SetCookieHeaderValue.Parse(header);

        Assert.Equal("refresh_token", cookie.Name.ToString());
        Assert.False(string.IsNullOrEmpty(cookie.Value.ToString()));
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal("/api/auth", cookie.Path.ToString());
        Assert.NotNull(cookie.Expires);
        return cookie;
    }

    private async Task<(string Email, Guid Id)> RegisterUser()
    {
        var email = $"login-{Guid.NewGuid():N}@example.com";
        using var form = new MultipartFormDataContent
        {
            { new StringContent(email), "email" },
            { new StringContent(ValidPassword), "password" },
            { new StringContent("Test"), "firstName" },
            { new StringContent("User"), "lastName" }
        };

        var response = await Client.PostAsync("/api/auth/register", form, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return (email, body.RootElement.GetProperty("id").GetGuid());
    }

    // Stands in for a future OAuth-only account: a user row with no user_credentials row.
    private async Task<User> AddUserWithoutPassword()
    {
        var user = User.Register($"nopass-{Guid.NewGuid():N}@example.com", "No", "Password", DateTimeOffset.UtcNow);

        using var scope = fixture.Migrated.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        return user;
    }

    private async Task<List<RefreshToken>> RefreshTokensOf(Guid userId)
    {
        using var scope = fixture.Migrated.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        return await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == userId).ToListAsync(Ct);
    }

    private Task<HttpResponseMessage> Login(string email, string password) =>
        Client.PostAsJsonAsync("/api/auth/login", new { email, password }, Ct);

    private Task<HttpResponseMessage> PostJson(string json) =>
        Client.PostAsync("/api/auth/login", new StringContent(json, Encoding.UTF8, "application/json"), Ct);

    private static async Task AssertProblem(HttpResponseMessage response, string code)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("traceId").GetString()));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
