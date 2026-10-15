namespace Auth.Domain;

/// <summary>
/// One refresh token in a rotation family (AD-8). Only the SHA-256 hash of the raw token is
/// kept; the raw value lives only in the client's cookie. Rotation, reuse and revocation
/// (<see cref="ReplacedById"/>, <see cref="ConsumedAt"/>, <see cref="RevokedAt"/>) arrive with
/// refresh and logout; login only starts a family.
/// </summary>
public class RefreshToken
{
    /// <summary>Each token is valid for 30 days from issue (sliding across rotations).</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    /// <summary>No token in a family outlives this cap, counted from the login that started it.</summary>
    public static readonly TimeSpan FamilyLifetime = TimeSpan.FromDays(90);

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public Guid? ReplacedById { get; private set; }

    public DateTimeOffset FamilyExpiresAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    // Required by EF Core to materialize rows.
    private RefreshToken()
    {
    }

    /// <summary>
    /// The first token of a new family, issued at login. Every login starts its own family, so
    /// sessions on different devices rotate and are revoked independently.
    /// </summary>
    public static RefreshToken StartFamily(Guid userId, string tokenHash, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            FamilyId = Guid.CreateVersion7(),
            TokenHash = tokenHash,
            ExpiresAt = now + Lifetime,
            FamilyExpiresAt = now + FamilyLifetime
        };
}
