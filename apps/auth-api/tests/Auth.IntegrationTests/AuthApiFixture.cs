using System.Security.Cryptography;
using Auth.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Auth.IntegrationTests;

/// <summary>
/// One real PostgreSQL 18 per test class (AD-20) plus the API in-process against it. The
/// schema is applied with MigrateAsync here only; app code never migrates itself (AD-12).
/// Both factories get a throwaway RS256 signing key, written once to a temp PEM, because the
/// app refuses to start without one.
/// </summary>
public sealed class AuthApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    private readonly string _signingKeyPath = Path.Combine(Path.GetTempPath(), $"auth-tests-{Guid.NewGuid():N}.pem");

    /// <summary>The public half of the signing key, to validate issued tokens.</summary>
    public RSAParameters SigningPublicKey { get; private set; }

    public WebApplicationFactory<Program> Migrated { get; private set; } = null!;

    public WebApplicationFactory<Program> Unmigrated { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        using (var rsa = RSA.Create(2048))
        {
            await File.WriteAllTextAsync(_signingKeyPath, rsa.ExportPkcs8PrivateKeyPem());
            SigningPublicKey = rsa.ExportParameters(includePrivateParameters: false);
        }

        await _postgres.StartAsync();

        var migratedConnection = _postgres.GetConnectionString();
        Migrated = CreateFactory(migratedConnection);
        using (var scope = Migrated.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
        }

        // A second, empty database: reachable but with every migration still pending.
        var emptyConnection = new NpgsqlConnectionStringBuilder(migratedConnection) { Database = "auth_empty" }
            .ConnectionString;
        await using (var connection = new NpgsqlConnection(migratedConnection))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("CREATE DATABASE auth_empty", connection);
            await command.ExecuteNonQueryAsync();
        }

        Unmigrated = CreateFactory(emptyConnection);
    }

    public async ValueTask DisposeAsync()
    {
        if (Migrated is not null) await Migrated.DisposeAsync();
        if (Unmigrated is not null) await Unmigrated.DisposeAsync();
        await _postgres.DisposeAsync();
        File.Delete(_signingKeyPath);
    }

    private WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:Postgres", connectionString)
            .UseSetting("Jwt:SigningKeyPath", _signingKeyPath));
}
