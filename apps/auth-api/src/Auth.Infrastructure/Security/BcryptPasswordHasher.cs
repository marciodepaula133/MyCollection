using Auth.Application.Abstractions;

namespace Auth.Infrastructure.Security;

/// <summary>
/// The Enhanced variants SHA-384 pre-hash the password, which removes bcrypt's 72-byte
/// truncation. Never swap in plain HashPassword/Verify: hashes would no longer match.
/// </summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.EnhancedHashPassword(password);

    public bool Verify(string password, string passwordHash) =>
        BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);
}
