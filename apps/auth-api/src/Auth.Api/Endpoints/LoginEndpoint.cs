using Auth.Application.Features.Login;
using HttpResults = Microsoft.AspNetCore.Http.Results;
using MyCollection.ServiceDefaults;

namespace Auth.Api.Endpoints;

public static class LoginEndpoint
{
    public static IEndpointRouteBuilder MapLogin(this IEndpointRouteBuilder group)
    {
        group.MapPost("/login", HandleAsync)
            .Produces<TokenResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        return group;
    }

    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        LoginUseCase useCase,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(new LoginCommand(request.Email!, request.Password!), cancellationToken);

        return result.Match(
            login =>
            {
                // The body carries a bearer token: no cache may keep it (RFC 6749 §5.1).
                httpContext.Response.Headers.CacheControl = "no-store";
                RefreshTokenCookie.Append(httpContext.Response, login.RefreshToken, login.RefreshExpiresAt);
                return HttpResults.Ok(new TokenResponse(login.AccessToken, login.ExpiresAt));
            },
            error => error.ToProblem(httpContext));
    }
}
