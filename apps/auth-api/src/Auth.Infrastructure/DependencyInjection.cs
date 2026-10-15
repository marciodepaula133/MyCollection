using Auth.Application.Abstractions;
using Auth.Application.Features.Login;
using Auth.Application.Features.Register;
using Auth.Infrastructure.Repositories;
using Auth.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Auth.Infrastructure;

public static class DependencyInjection
{
    /// <param name="validateOnStart">
    /// Whether host start fails when required config (the JWT signing key) is missing. The
    /// composition root passes false only while the OpenAPI generator runs the app (AD-16).
    /// </param>
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services, bool validateOnStart)
    {
        // The connection string is read when the context is first resolved, not at startup,
        // so the OpenAPI generator can boot the app without a database.
        services.AddDbContext<AuthDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton(TimeProvider.System);

        // The signing key must exist before the app serves traffic (AD-6), so a missing one stops
        // host start. The key itself is read on first use.
        var jwtOptions = services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .PostConfigure<IHostEnvironment>((options, environment) =>
            {
                if (!string.IsNullOrWhiteSpace(options.SigningKeyPath))
                {
                    options.SigningKeyPath = Path.GetFullPath(options.SigningKeyPath, environment.ContentRootPath);
                }
            });
        if (validateOnStart)
        {
            jwtOptions.ValidateOnStart();
        }

        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton<RsaSigningKey>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();

        services.AddScoped<RegisterUseCase>();
        services.AddScoped<LoginUseCase>();

        // Ready means "the schema is migrated". The app never migrates itself (AD-12), so a
        // pending migration keeps it not-ready until the auth-migrate bundle has run.
        services.AddHealthChecks().AddDbContextCheck<AuthDbContext>(
            "migrations",
            customTestQuery: async (db, ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any());

        return services;
    }
}
