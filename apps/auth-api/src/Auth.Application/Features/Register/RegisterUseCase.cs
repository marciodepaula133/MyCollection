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
        var email = User.NormalizeEmail(command.Email);

        // Checked before hashing so a known duplicate skips the bcrypt cost.
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return EmailAlreadyRegistered;
        }

        var user = User.Register(email, command.FirstName, command.LastName, timeProvider.GetUtcNow());
        await users.AddAsync(user, passwordHasher.Hash(command.Password), cancellationToken);

        // A concurrent registration can pass the check above too; the unique index on
        // users.email rejects the loser, which gets the same conflict.
        if (await unitOfWork.CommitAsync(cancellationToken) == CommitOutcome.UniqueViolation)
        {
            return EmailAlreadyRegistered;
        }

        return new RegisterResult(user.Id, user.Email, user.FirstName!, user.LastName!);
    }
}
