using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Endpoints;

// multipart/form-data, not JSON: Avatar is an uploaded file. A settable-property class, not a
// positional record: ASP.NET Core's form complex-type binder treats every record constructor
// parameter as required to be *present* in the form (throwing a raw BadHttpRequestException
// otherwise, bypassing AddValidation() entirely) regardless of nullability. Properties left
// unset by the binder keep their default (null), which [Required] then catches normally.
public sealed class RegisterRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string? Email { get; init; }

    [Required, StringLength(32, MinimumLength = 8)]
    public string? Password { get; init; }

    [Required, MaxLength(100)]
    public string? FirstName { get; init; }

    [Required, MaxLength(100)]
    public string? LastName { get; init; }

    // Accepted for forward-compat (a future avatar-upload flow) but not read or persisted yet:
    // Auth.Domain.User.AvatarUrl stays unset from this endpoint.
    public IFormFile? Avatar { get; init; }
}
