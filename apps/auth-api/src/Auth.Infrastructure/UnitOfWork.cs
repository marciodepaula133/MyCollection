using Auth.Application.Abstractions;
using Auth.Application.Features.Register;
using Auth.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using MyCollection.Results;
using Npgsql;

namespace Auth.Infrastructure;

public sealed class UnitOfWork(AuthDbContext db) : IUnitOfWork
{
    public async Task<Error?> CommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            // One SaveChanges is one transaction: the user and its credentials land together.
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException ex) when (IsEmailUniqueViolation(ex))
        {
            return RegisterUseCase.EmailAlreadyRegistered;
        }
    }

    // 23505 = unique_violation. Matching the constraint name keeps any other unique
    // violation from being mislabelled as a duplicate email.
    private static bool IsEmailUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UserConfiguration.EmailIndexName
        };
}
