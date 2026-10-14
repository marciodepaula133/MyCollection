using Auth.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Auth.Infrastructure;

public sealed class UnitOfWork(AuthDbContext db) : IUnitOfWork
{
    public async Task<CommitOutcome> CommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            // One SaveChanges is one transaction: everything staged lands together or not at all.
            await db.SaveChangesAsync(cancellationToken);
            return CommitOutcome.Committed;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
        {
            // Report the fact only; the use case decides what the conflict means.
            db.ChangeTracker.Clear();
            return CommitOutcome.UniqueViolation;
        }
    }
}
