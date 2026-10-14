namespace Auth.Application.Features.Register;

public sealed record RegisterCommand(string Email, string Password, string FirstName, string LastName);

public sealed record RegisterResult(Guid Id, string Email, string FirstName, string LastName);
