using Auth.Domain;

namespace Auth.Application.Abstractions;

public interface IUserRepository
{
    /// <summary>
    /// Stages the user and its credentials. Nothing is written until
    /// <see cref="IUnitOfWork.CommitAsync"/>.
    /// </summary>
    Task AddAsync(User user, string passwordHash, CancellationToken cancellationToken);
}
