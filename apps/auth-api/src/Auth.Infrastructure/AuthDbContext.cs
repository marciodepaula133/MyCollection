using Microsoft.EntityFrameworkCore;

namespace Auth.Infrastructure;

/// <summary>
/// Empty for this setup slice. AD-12 needs a real DbContext to wire the migration-bundle
/// mechanism (dotnet-ef, IDesignTimeDbContextFactory) ahead of CAP-1, which adds the first
/// entity, DbSet and migration.
/// </summary>
public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options)
        : base(options)
    {
    }
}
