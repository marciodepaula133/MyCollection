namespace Auth.Application.Features.Register;

public sealed record RegisterCommand(string Email, string Password);

public sealed record RegisterResult(Guid Id, string Email);
