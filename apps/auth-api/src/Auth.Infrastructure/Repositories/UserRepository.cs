using Auth.Application.Abstractions;
using Auth.Domain;
using Auth.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auth.Infrastructure.Repositories;

public sealed class UserRepository(AuthDbContext db) : IUserRepository
{
    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<LoginCandidate?> FindForLoginAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        // Left join: a user without a password credential is still found, with a null hash.
        (from user in db.Users.AsNoTracking()
         where user.Email == normalizedEmail
         join credential in db.UserCredentials on user.Id equals credential.UserId into credentials
         from credential in credentials.DefaultIfEmpty()
         select new LoginCandidate(user, credential != null ? credential.PasswordHash : null))
        .SingleOrDefaultAsync(cancellationToken);

    public Task AddAsync(User user, string passwordHash, CancellationToken cancellationToken)
    {
        db.Users.Add(user);
        db.UserCredentials.Add(new UserCredential
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            PasswordHash = passwordHash
        });

        return Task.CompletedTask;
    }
}
