using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Auth.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Auth.IntegrationTests;

public class RegisterEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private const string ValidPassword = "s3cret-pass";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private HttpClient Client => fixture.Migrated.CreateClient();

    [Fact]
    public async Task Happy_path_creates_user_and_hashed_credentials()
    {
        var email = $"happy-{Guid.NewGuid():N}@example.com";

        var response = await Register(email, ValidPassword);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(email, body.RootElement.GetProperty("email").GetString());
        var id = body.RootElement.GetProperty("id").GetGuid();
        Assert.False(body.RootElement.TryGetProperty("password", out _));
        Assert.False(body.RootElement.TryGetProperty("passwordHash", out _));

        using var scope = fixture.Migrated.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == id, Ct);
        Assert.Equal(email, user.Email);
        Assert.Null(user.EmailVerifiedAt);

        var credential = await db.UserCredentials.SingleAsync(c => c.UserId == id, Ct);
        Assert.NotEqual(ValidPassword, credential.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.EnhancedVerify(ValidPassword, credential.PasswordHash));
    }

    [Theory]
    [InlineData(" {0} ")]
    [InlineData("{0}")]
    public async Task Duplicate_email_in_any_casing_or_whitespace_is_a_409(string variant)
    {
        var local = $"dup-{Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.Created, (await Register($"{local}@example.com", ValidPassword)).StatusCode);

        var second = string.Format(variant, $"{local.ToUpperInvariant()}@Example.com");
        var response = await Register(second, ValidPassword);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblem(response, "email_already_registered");
    }

    [Fact]
    public async Task Email_longer_than_254_characters_is_a_400_not_a_database_error()
    {
        var response = await Register($"{new string('a', 250)}@example.com", ValidPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblem(response, "validation_failed");
    }

    [Fact]
    public async Task Concurrent_duplicate_registrations_yield_one_201_and_one_409()
    {
        var email = $"race-{Guid.NewGuid():N}@example.com";

        var responses = await Task.WhenAll(Register(email, ValidPassword), Register(email, ValidPassword));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Email_is_trimmed_and_lower_cased_before_storage()
    {
        var local = $"norm-{Guid.NewGuid():N}";

        var response = await Register($" {local.ToUpperInvariant()}@Example.COM ", ValidPassword);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal($"{local}@example.com", body.RootElement.GetProperty("email").GetString());
    }

    [Theory]
    [InlineData("not-an-email", ValidPassword)]
    [InlineData("", ValidPassword)]
    [InlineData("a@b.com", "short")]
    [InlineData("a@b.com", "123456789012345678901234567890123")] // 33 characters
    public async Task Invalid_input_is_a_400_validation_failed(string email, string password)
    {
        var response = await Register(email, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblem(response, "validation_failed");
    }

    [Theory]
    [InlineData("12345678")] // 8 characters: the lower bound
    [InlineData("12345678901234567890123456789012")] // 32 characters: the upper bound
    public async Task Password_length_bounds_are_inclusive(string password)
    {
        var response = await Register($"bound-{Guid.NewGuid():N}@example.com", password);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ "password": "s3cret-pass" }""")]
    [InlineData("""{ "email": "a@b.com" }""")]
    [InlineData("""{ "email": null, "password": null }""")]
    public async Task Missing_or_null_fields_are_a_400_validation_failed(string json)
    {
        var response = await Client.PostAsync(
            "/api/auth/register",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblem(response, "validation_failed");
    }

    private Task<HttpResponseMessage> Register(string email, string password) =>
        Client.PostAsJsonAsync("/api/auth/register", new { email, password }, Ct);

    private static async Task AssertProblem(HttpResponseMessage response, string code)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("traceId").GetString()));
    }
}
