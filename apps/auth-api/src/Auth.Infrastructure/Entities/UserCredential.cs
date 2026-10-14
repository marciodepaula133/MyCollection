namespace Auth.Infrastructure.Entities;

/// <summary>
/// The password hash for a user, kept apart from the Domain <c>User</c> so the hash never
/// travels with the entity. One row per user.
/// </summary>
public class UserCredential
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string PasswordHash { get; set; } = string.Empty;
}
