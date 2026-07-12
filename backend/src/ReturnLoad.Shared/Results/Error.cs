namespace ReturnLoad.Shared.Results;

/// <summary>
/// A domain- or application-level error, identified by a stable machine-readable
/// <see cref="Code"/> and a human-readable <see cref="Message"/>. Modelling errors
/// as values (rather than raw strings or exceptions) keeps failure handling
/// explicit and typed at every boundary (01_PROJECT_RULES.md §1, §4).
/// </summary>
public sealed record Error(string Code, string Message)
{
    /// <summary>The sentinel used by a successful <see cref="Result"/>: "no error".</summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>Input failed validation.</summary>
    public static Error Validation(string message) => new("validation_error", message);

    /// <summary>A requested resource does not exist.</summary>
    public static Error NotFound(string message) => new("not_found", message);

    /// <summary>The action conflicts with the current state.</summary>
    public static Error Conflict(string message) => new("conflict", message);

    /// <summary>The caller is not permitted to perform the action.</summary>
    public static Error Unauthorized(string message) => new("unauthorized", message);
}
