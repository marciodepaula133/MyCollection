using Auth.Domain;

namespace Auth.Application.Abstractions;

public interface IRefreshTokenStore
{
    /// <summary>
    /// Stages the token. Nothing is written until <see cref="IUnitOfWork.CommitAsync"/>.
    /// </summary>
    void Add(RefreshToken refreshToken);
}
