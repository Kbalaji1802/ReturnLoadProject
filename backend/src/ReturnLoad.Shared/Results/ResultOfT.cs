namespace ReturnLoad.Shared.Results;

/// <summary>
/// A <see cref="Result"/> that carries a <typeparamref name="TValue"/> on success.
/// The value is only accessible when <see cref="Result.IsSuccess"/> is true;
/// accessing it on a failure throws, so callers must check the outcome first.
/// </summary>
/// <typeparam name="TValue">The type produced on success.</typeparam>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    private Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
        => _value = value;

    /// <summary>
    /// The success value. Throws <see cref="InvalidOperationException"/> if the
    /// result is a failure — a failed result has no value to return.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static Result<TValue> Success(TValue value) => new(value, true, Error.None);

    // 'new' intentionally hides Result.Failure(Error): this typed overload returns
    // Result<TValue> so callers keep the value type through the failure path.
    public static new Result<TValue> Failure(Error error) => new(default, false, error);

    /// <summary>Lifts a value into a successful result.</summary>
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>Lifts an error into a failed result.</summary>
    public static implicit operator Result<TValue>(Error error) => Failure(error);
}
