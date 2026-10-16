namespace Auth.Domain;

/// <summary>
/// An account. Holds identity data only: credentials (the password hash) are an
/// Infrastructure concern and never live on this entity.
/// </summary>
public class User
{
    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    public string? AvatarUrl { get; private set; }

    public DateTimeOffset? EmailVerifiedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    // Required by EF Core to materialize rows.
    private User()
    {
    }

    /// <summary>
    /// The form every email is stored and compared in: trimmed and lower-cased. Every sign-up
    /// and sign-in path normalizes with this before storing or looking up a user.
    /// </summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// Creates a password sign-up. <paramref name="normalizedEmail"/> must already be normalized
    /// with <see cref="NormalizeEmail"/>; <see cref="EmailVerifiedAt"/> stays null until email verification
    /// exists (AD-10). <paramref name="firstName"/> and <paramref name="lastName"/> are mandatory for this sign-up path, even though
    /// the column stays nullable for other paths (e.g. future OAuth sign-ups).
    /// </summary>
    public static User Register(string normalizedEmail, string firstName, string lastName, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Email = normalizedEmail,
            FirstName = firstName,
            LastName = lastName,
            CreatedAt = now,
            UpdatedAt = now
        };
}
