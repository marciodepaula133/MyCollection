using System.Net.Mail;
using Auth.Application.Features.Register;
using MyCollection.Results;
using HttpResults = Microsoft.AspNetCore.Http.Results;
using MyCollection.ServiceDefaults;

namespace Auth.Api.Endpoints;

// Nullable so a missing field binds as null and becomes a 400 here, not a framework error.
public sealed record RegisterRequest(string? Email, string? Password);

public sealed record RegisterResponse(Guid Id, string Email);

public static class RegisterEndpoint
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 32;
    private const int EmailMaxLength = 254;

    public static IEndpointRouteBuilder MapRegister(this IEndpointRouteBuilder group)
    {
        group.MapPost("/register", HandleAsync)
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> HandleAsync(
        RegisterRequest request,
        RegisterUseCase useCase,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return validationError.ToProblem(httpContext);
        }

        var result = await useCase.ExecuteAsync(
            new RegisterCommand(request.Email!, request.Password!),
            cancellationToken);

        return result.Match(
            // No Location header: there is no GET-user route in this slice to point at.
            registered => HttpResults.Json(
                new RegisterResponse(registered.Id, registered.Email),
                statusCode: StatusCodes.Status201Created),
            error => error.ToProblem(httpContext));
    }

    private static Error? Validate(RegisterRequest request)
    {
        if (!IsValidEmail(request.Email))
        {
            return new Error("validation_failed", ErrorKind.Validation, "Email is missing or malformed.");
        }

        // The password is measured as sent: no trimming, no composition rules.
        if (request.Password is not { Length: >= PasswordMinLength and <= PasswordMaxLength })
        {
            return new Error(
                "validation_failed",
                ErrorKind.Validation,
                $"Password must be {PasswordMinLength} to {PasswordMaxLength} characters.");
        }

        return null;
    }

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var trimmed = email.Trim();
        return trimmed.Length <= EmailMaxLength
            && MailAddress.TryCreate(trimmed, out var parsed)
            && parsed.Address == trimmed;
    }
}
