namespace ReturnLoad.Shared.Configuration;

/// <summary>
/// Upload constraints, bound from the "FileUpload" section. These bound what the
/// Documents context (M6) will accept — a size ceiling plus MIME/extension allowlists
/// so only expected document types are stored (03_TECHNICAL_BIBLE.md §7). Defined in
/// Shared so both the API host and the Application layer can enforce it via
/// <see cref="ReturnLoad.Shared.Api.FileUploadValidator"/>. M1.5 supplies the policy and
/// validator only; no upload endpoint exists yet.
/// </summary>
public sealed class FileUploadOptions
{
    public const string SectionName = "FileUpload";

    /// <summary>Maximum accepted file size in bytes. Default 10 MB.</summary>
    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;

    /// <summary>Permitted MIME content types (exact match, case-insensitive).</summary>
    public string[] AllowedMimeTypes { get; init; } = ["application/pdf", "image/jpeg", "image/png"];

    /// <summary>Permitted file extensions, leading dot, lowercase (e.g. ".pdf").</summary>
    public string[] AllowedExtensions { get; init; } = [".pdf", ".jpg", ".jpeg", ".png"];
}
