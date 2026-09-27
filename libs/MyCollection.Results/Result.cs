namespace MyCollection.Results;

/// <summary>
/// The outcome of a use case: either a value, or a typed <see cref="Results.Error"/> for an
/// expected failure. Use cases throw only for bugs, never for expected failures (AD-13).
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public T Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException("Cannot access Value of a failed result.");

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
        Error = null;
    }

    private Result(Error error)
    {
        _value = default;
        IsSuccess = false;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(Error!);
}
