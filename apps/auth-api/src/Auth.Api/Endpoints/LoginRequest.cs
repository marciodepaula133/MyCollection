using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Endpoints;

// JSON body. No minimum password length here: the length policy belongs to register, and a
// login only has to match. The upper bound keeps an oversized body away from bcrypt.
public sealed record LoginRequest(
    [property: Required, EmailAddress] string? Email,
    [property: Required, MaxLength(1024)] string? Password);
