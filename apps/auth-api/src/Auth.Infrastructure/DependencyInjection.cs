using Auth.Application.Abstractions;
using Auth.Application.Features.Register;
using Auth.Infrastructure.Repositories;
using Auth.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services)
    {
        // The connection string is read when the context is first resolved, not at startup,
        // so the OpenAPI generator can boot the app without a database.
        services.AddDbContext<AuthDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<RegisterUseCase>();

        // Ready means "the schema is migrated". The app never migrates itself (AD-12), so a
        // pending migration keeps it not-ready until the auth-migrate bundle has run.
        services.AddHealthChecks().AddDbContextCheck<AuthDbContext>(
            "migrations",
            customTestQuery: async (db, ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any());

        return services;
    }
}
