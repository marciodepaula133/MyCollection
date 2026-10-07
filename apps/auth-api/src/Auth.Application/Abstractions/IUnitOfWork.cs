using MyCollection.Results;

namespace Auth.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>
    /// Writes everything staged by the repositories in one transaction. Returns the expected
    /// persistence failure (for example a unique-constraint conflict) as an <see cref="Error"/>,
    /// or null on success.
    /// </summary>
    Task<Error?> CommitAsync(CancellationToken cancellationToken);
}
