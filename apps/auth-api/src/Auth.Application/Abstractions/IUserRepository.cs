using Auth.Domain;

namespace Auth.Application.Abstractions;

public interface IUserRepository
{
    /// <summary>Whether a user with this already-normalized email exists.</summary>
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>
    /// The user with this already-normalized email and their password hash, or null when no
    /// user has that email. <see cref="LoginCandidate.PasswordHash"/> is null for a user with
    /// no password credential.
    /// </summary>
    Task<LoginCandidate?> FindForLoginAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>
    /// Stages the user and its credentials. Nothing is written until
    /// <see cref="IUnitOfWork.CommitAsync"/>.
    /// </summary>
    Task AddAsync(User user, string passwordHash, CancellationToken cancellationToken);
}

public sealed record LoginCandidate(User User, string? PasswordHash);
