namespace ReturnLoad.Shared.Api;

/// <summary>
/// A single, structured error inside an <see cref="ApiResponse{TData}"/>. Every
/// failure the API returns — validation, not-found, conflict, unexpected — is
/// expressed as one or more of these, so clients handle errors uniformly
/// (03_TECHNICAL_BIBLE.md §6).
/// </summary>
/// <param name="Field">
/// The offending input field for validation errors (e.g. <c>"email"</c>), or
/// <see langword="null"/> for errors that are not tied to a specific field.
/// </param>
/// <param name="Code">
/// A stable, machine-readable code (e.g. <c>"INVALID_EMAIL"</c>) that clients can
/// branch on without parsing the human message. See <see cref="ErrorCodes"/>.
/// </param>
/// <param name="Message">A human-readable description safe to show to end users.</param>
public sealed record ApiError(string? Field, string Code, string Message)
{
    /// <summary>A field-level validation error.</summary>
    public static ApiError Validation(string field, string message, string code = ErrorCodes.ValidationError) =>
        new(field, code, message);

    /// <summary>An error not tied to a specific input field.</summary>
    public static ApiError General(string code, string message) => new(null, code, message);
}
