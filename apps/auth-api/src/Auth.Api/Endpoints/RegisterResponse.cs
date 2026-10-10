namespace Auth.Api.Endpoints;

public sealed record RegisterResponse(Guid Id, string Email, string FirstName, string LastName);
