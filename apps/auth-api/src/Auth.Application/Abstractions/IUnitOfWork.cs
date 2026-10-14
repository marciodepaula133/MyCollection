namespace Auth.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>
    /// Writes everything staged by the repositories in one transaction. Reports an expected
    /// persistence conflict as a <see cref="CommitOutcome"/> instead of throwing; the use case
    /// decides what that conflict means for its feature.
    /// </summary>
    Task<CommitOutcome> CommitAsync(CancellationToken cancellationToken);
}

public enum CommitOutcome
{
    Committed,

    /// <summary>A unique constraint rejected the write; nothing was saved.</summary>
    UniqueViolation
}
