using Auth.Application.Abstractions;
using Auth.Domain;
using Auth.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auth.Infrastructure.Repositories;

public sealed class UserRepository(AuthDbContext db) : IUserRepository
{
    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

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
