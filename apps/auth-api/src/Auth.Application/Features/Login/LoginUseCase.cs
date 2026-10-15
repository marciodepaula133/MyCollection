using Auth.Application.Abstractions;
using Auth.Application.Features.Register;
using Auth.Domain;
using MyCollection.Results;

namespace Auth.Application.Features.Login;

public sealed class LoginUseCase(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    IRefreshTokenGenerator refreshTokenGenerator,
    IRefreshTokenStore refreshTokens,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    // One error for every failure, so a response never reveals whether the email is registered.
    public static readonly Error InvalidCredentials = new(
        "invalid_credentials",
        ErrorKind.Unauthorized,
        "Invalid email or password.");

    public async Task<Result<LoginResult>> ExecuteAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var candidate = await users.FindForLoginAsync(
            RegisterUseCase.NormalizeEmail(command.Email), cancellationToken);

        // Unknown email or no password credential: fail without a bcrypt check, as ASP.NET Core
        // Identity's PasswordSignInAsync does.
        if (candidate?.PasswordHash is not { } passwordHash
            || !passwordHasher.Verify(command.Password, passwordHash))
        {
            return InvalidCredentials;
        }

        var now = timeProvider.GetUtcNow();
        var accessToken = tokenIssuer.Issue(candidate.User, now);
        var refreshToken = refreshTokenGenerator.Generate();
        var stored = RefreshToken.StartFamily(candidate.User.Id, refreshToken.Hash, now);

        refreshTokens.Add(stored);

        // The only unique index touched is token_hash, and a 256-bit random token never
        // collides: a violation here is a bug, not an expected outcome.
        if (await unitOfWork.CommitAsync(cancellationToken) != CommitOutcome.Committed)
        {
            throw new InvalidOperationException("Refresh token hash collided on insert.");
        }

        return new LoginResult(accessToken.Token, accessToken.ExpiresAt, refreshToken.Raw, stored.ExpiresAt);
    }
}
