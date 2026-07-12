namespace ReturnLoad.Shared.Results;

/// <summary>
/// The outcome of an operation that can succeed or fail without throwing.
/// Encourages explicit, non-exceptional error handling at boundaries
/// (01_PROJECT_RULES.md §1). Use <see cref="Result{TValue}"/> when a value is
/// produced on success.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        // A result must be internally consistent: success carries no error,
        // failure always carries one.
        switch (isSuccess)
        {
            case true when error != Error.None:
                throw new ArgumentException("A successful result cannot carry an error.", nameof(error));
            case false when error == Error.None:
                throw new ArgumentException("A failed result must carry an error.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);
}
