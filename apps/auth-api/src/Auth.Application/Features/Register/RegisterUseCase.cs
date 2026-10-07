using Auth.Application.Abstractions;
using Auth.Domain;
using MyCollection.Results;

namespace Auth.Application.Features.Register;

public sealed class RegisterUseCase(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public static readonly Error EmailAlreadyRegistered = new(
        "email_already_registered",
        ErrorKind.Conflict,
        "An account with this email already exists.");

    public async Task<Result<RegisterResult>> ExecuteAsync(
        RegisterCommand command,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(command.Email);

        var user = User.Register(email, timeProvider.GetUtcNow());
        await users.AddAsync(user, passwordHasher.Hash(command.Password), cancellationToken);

        // No existence pre-check: the unique index is the race-safe source of truth, and
        // Infrastructure reports a violation as EmailAlreadyRegistered.
        var failure = await unitOfWork.CommitAsync(cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        return new RegisterResult(user.Id, user.Email);
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
