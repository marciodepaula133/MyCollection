using System.Net;
using System.Text;
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
        Assert.Equal("Test", body.RootElement.GetProperty("firstName").GetString());
        Assert.Equal("User", body.RootElement.GetProperty("lastName").GetString());
        Assert.False(body.RootElement.TryGetProperty("name", out _));
        var id = body.RootElement.GetProperty("id").GetGuid();
        Assert.False(body.RootElement.TryGetProperty("password", out _));
        Assert.False(body.RootElement.TryGetProperty("passwordHash", out _));

        using var scope = fixture.Migrated.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == id, Ct);
        Assert.Equal(email, user.Email);
        Assert.Equal("Test", user.FirstName);
        Assert.Equal("User", user.LastName);
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

    [Fact]
    public async Task Legacy_name_field_only_is_a_400_validation_failed()
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent($"noname-{Guid.NewGuid():N}@example.com"), "email" },
            { new StringContent(ValidPassword), "password" },
            { new StringContent("Test User"), "name" }
        };

        var response = await Client.PostAsync("/api/auth/register", form, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblem(response, "validation_failed");
    }

    [Fact]
    public async Task Avatar_file_is_accepted_but_not_persisted()
    {
        var email = $"avatar-{Guid.NewGuid():N}@example.com";

        using var form = new MultipartFormDataContent
        {
            { new StringContent(email), "email" },
            { new StringContent(ValidPassword), "password" },
            { new StringContent("Test"), "firstName" },
            { new StringContent("User"), "lastName" }
        };
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("not-really-a-png"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "avatar", "avatar.png");

        var response = await Client.PostAsync("/api/auth/register", form, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var id = body.RootElement.GetProperty("id").GetGuid();

        using var scope = fixture.Migrated.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == id, Ct);
        Assert.Null(user.AvatarUrl);
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
    [InlineData(null, "s3cret-pass", "Test", "User")]
    [InlineData("a@b.com", null, "Test", "User")]
    [InlineData("a@b.com", "s3cret-pass", null, "User")]
    [InlineData("a@b.com", "s3cret-pass", "Test", null)]
    [InlineData("a@b.com", "s3cret-pass", "", "User")]
    [InlineData("a@b.com", "s3cret-pass", "Test", "")]
    [InlineData("a@b.com", "s3cret-pass", "   ", "User")]
    [InlineData("a@b.com", "s3cret-pass", "Test", "   ")]
    public async Task Missing_or_empty_fields_are_a_400_validation_failed(string? email, string? password, string? firstName, string? lastName)
    {
        using var form = new MultipartFormDataContent();
        if (email is not null)
        {
            form.Add(new StringContent(email), "email");
        }

        if (password is not null)
        {
            form.Add(new StringContent(password), "password");
        }

        if (firstName is not null)
        {
            form.Add(new StringContent(firstName), "firstName");
        }

        if (lastName is not null)
        {
            form.Add(new StringContent(lastName), "lastName");
        }

        var response = await Client.PostAsync("/api/auth/register", form, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblem(response, "validation_failed");
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public async Task First_name_length_bound_is_100(int length, bool accepted)
    {
        var response = await Register($"len-{Guid.NewGuid():N}@example.com", ValidPassword, new string('a', length), "User");

        await AssertAcceptedOrRejected(response, accepted, "FirstName");
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public async Task Last_name_length_bound_is_100(int length, bool accepted)
    {
        var response = await Register($"len-{Guid.NewGuid():N}@example.com", ValidPassword, "Test", new string('a', length));

        await AssertAcceptedOrRejected(response, accepted, "LastName");
    }

    [Fact]
    public async Task Multiple_invalid_fields_are_all_reported_by_property_name()
    {
        var response = await Register("not-an-email", "short", "", "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("validation_failed", body.RootElement.GetProperty("code").GetString());
        var errors = body.RootElement.GetProperty("errors");
        foreach (var key in new[] { "Email", "Password", "FirstName", "LastName" })
        {
            Assert.True(errors.TryGetProperty(key, out _), $"errors is missing {key}");
        }
    }

    [Fact]
    public async Task Json_body_is_a_415_unsupported_media_type()
    {
        using var content = new StringContent(
            $$"""{"email":"json-{{Guid.NewGuid():N}}@example.com","password":"{{ValidPassword}}","firstName":"Test","lastName":"User"}""",
            Encoding.UTF8,
            "application/json");

        var response = await Client.PostAsync("/api/auth/register", content, Ct);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        await AssertProblem(response, "unsupported_media_type");
    }

    private static async Task AssertAcceptedOrRejected(HttpResponseMessage response, bool accepted, string errorKey)
    {
        if (accepted)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return;
        }

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblem(response, "validation_failed");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty(errorKey, out _));
    }

    private Task<HttpResponseMessage> Register(
        string email, string password, string firstName = "Test", string lastName = "User")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(email), "email" },
            { new StringContent(password), "password" },
            { new StringContent(firstName), "firstName" },
            { new StringContent(lastName), "lastName" }
        };
        return Client.PostAsync("/api/auth/register", form, Ct);
    }

    private static async Task AssertProblem(HttpResponseMessage response, string code)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("traceId").GetString()));
    }
}
