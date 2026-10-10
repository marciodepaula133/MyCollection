using Auth.Application.Features.Register;
using Microsoft.AspNetCore.Mvc;
using HttpResults = Microsoft.AspNetCore.Http.Results;
using MyCollection.ServiceDefaults;

namespace Auth.Api.Endpoints;

public static class RegisterEndpoint
{
    public static IEndpointRouteBuilder MapRegister(this IEndpointRouteBuilder group)
    {
        group.MapPost("/register", HandleAsync)
            .DisableAntiforgery()
            .Accepts<RegisterRequest>("multipart/form-data")
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> HandleAsync(
        [FromForm] RegisterRequest request,
        RegisterUseCase useCase,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new RegisterCommand(request.Email!, request.Password!, request.FirstName!, request.LastName!),
            cancellationToken);

        return result.Match(
            // No Location header: there is no GET-user route in this slice to point at.
            registered => HttpResults.Json(
                new RegisterResponse(registered.Id, registered.Email, registered.FirstName, registered.LastName),
                statusCode: StatusCodes.Status201Created),
            error => error.ToProblem(httpContext));
    }
}
