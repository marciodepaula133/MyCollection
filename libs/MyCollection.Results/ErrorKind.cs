namespace MyCollection.Results;

/// <summary>
/// The fixed set of error kinds a use case can fail with (AD-4).
/// ServiceDefaults owns the single mapping from each kind to an HTTP status (AD-13).
/// </summary>
public enum ErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    RateLimited,
    Unavailable
}
