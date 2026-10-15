using System.Security.Cryptography;
using Auth.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Auth.IntegrationTests;

/// <summary>
/// The signing-key startup check (AD-6). No database: the connection string is read lazily, so
/// these hosts start (or fail to) without Postgres.
/// </summary>
public sealed class SigningKeyStartupTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public void Missing_key_stops_startup_naming_the_path_and_the_generator()
    {
        var path = Path.Combine(Path.GetTempPath(), $"auth-missing-{Guid.NewGuid():N}.pem");

        AssertStartupFails(path);
    }

    [Fact]
    public void Unreadable_key_stops_startup_naming_the_path_and_the_generator()
    {
        var path = TempFile("this is not a PEM");

        AssertStartupFails(path);
    }

    [Fact]
    public void Relative_key_path_is_resolved_against_the_content_root()
    {
        var keyPath = TempFile(CreatePrivateKeyPem());

        string contentRoot;
        using (var probe = CreateFactory(keyPath))
        {
            contentRoot = probe.Services.GetRequiredService<IHostEnvironment>().ContentRootPath;
        }

        var relative = Path.GetRelativePath(contentRoot, keyPath);
        if (Path.IsPathRooted(relative))
        {
            Assert.Skip("The temp folder and the content root are on different drives.");
        }

        using var factory = CreateFactory(relative);

        var options = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        Assert.Equal(Path.GetFullPath(keyPath), options.SigningKeyPath);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            File.Delete(file);
        }
    }

    private static void AssertStartupFails(string path)
    {
        using var factory = CreateFactory(path);

        var exception = Record.Exception(() => factory.CreateClient());

        var validation = FindValidationException(exception);
        Assert.NotNull(validation);
        Assert.Contains(path, validation.Message);
        Assert.Contains("generate-dev-jwt-key", validation.Message);
    }

    private static OptionsValidationException? FindValidationException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is OptionsValidationException validation)
            {
                return validation;
            }

            if (exception is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    if (FindValidationException(inner) is { } found)
                    {
                        return found;
                    }
                }
            }

            exception = exception.InnerException;
        }

        return null;
    }

    private static WebApplicationFactory<Program> CreateFactory(string signingKeyPath) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("Jwt:SigningKeyPath", signingKeyPath));

    private static string CreatePrivateKeyPem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportPkcs8PrivateKeyPem();
    }

    private string TempFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"auth-key-{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, contents);
        _tempFiles.Add(path);
        return path;
    }
}
