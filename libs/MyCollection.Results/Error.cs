namespace MyCollection.Results;

/// <summary>
/// A typed, expected failure returned by a use case. <paramref name="Code"/> is a stable,
/// snake_case identifier owned by the service that raises it (e.g. "invalid_credentials"),
/// used verbatim as the ProblemDetails "code" (AD-13).
/// </summary>
public sealed record Error(string Code, ErrorKind Kind, string Message)
{
    public override string ToString() => $"{Code} ({Kind}): {Message}";
}
