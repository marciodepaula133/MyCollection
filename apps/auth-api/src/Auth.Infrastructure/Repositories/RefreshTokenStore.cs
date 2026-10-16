using Auth.Application.Abstractions;
using Auth.Domain;

namespace Auth.Infrastructure.Repositories;

public sealed class RefreshTokenStore(AuthDbContext db) : IRefreshTokenStore
{
    public void Add(RefreshToken refreshToken) => db.RefreshTokens.Add(refreshToken);
}
