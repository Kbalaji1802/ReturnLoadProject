using ReturnLoad.Shared.Configuration;

namespace ReturnLoad.Shared.Api;

/// <summary>
/// Validates a candidate file upload against <see cref="FileUploadOptions"/> — size,
/// MIME type, and extension allowlists (03_TECHNICAL_BIBLE.md §7). Returns the standard
/// <see cref="ApiError"/> list so callers surface failures in the normal envelope. The
/// Documents context (M6) calls this before persisting via
/// <c>IFileStorageService</c>; M1.5 ships only the contract, no endpoint.
/// </summary>
public static class FileUploadValidator
{
    /// <summary>Returns an empty list when the upload is acceptable, otherwise one error per violation.</summary>
    public static IReadOnlyList<ApiError> Validate(
        string fileName,
        string contentType,
        long sizeBytes,
        FileUploadOptions options)
    {
        List<ApiError> errors = [];

        if (sizeBytes <= 0)
        {
            errors.Add(ApiError.Validation("file", "The file is empty."));
        }
        else if (sizeBytes > options.MaxFileSizeBytes)
        {
            errors.Add(ApiError.Validation(
                "file",
                $"The file exceeds the maximum size of {options.MaxFileSizeBytes} bytes.",
                ErrorCodes.PayloadTooLarge));
        }

        if (!options.AllowedMimeTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(ApiError.Validation("file", $"The content type '{contentType}' is not allowed."));
        }

        string extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension)
            || !options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(ApiError.Validation("file", $"The file extension '{extension}' is not allowed."));
        }

        return errors;
    }
}
