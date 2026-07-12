namespace ReturnLoad.Infrastructure.Storage;

/// <summary>
/// Settings for the local-disk file storage backend, bound from the "FileStorage"
/// section. This is the development/default provider (ADR-0012); production swaps in a
/// cloud object store without changing business code.
/// </summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Directory under which files are stored. Relative paths resolve against the app base directory.</summary>
    public string RootPath { get; init; } = "storage";

    /// <summary>
    /// Base URL used to build temporary access URLs. For local disk this is a
    /// development convenience, not a cryptographically signed URL — real signing
    /// arrives with the cloud provider.
    /// </summary>
    public string PublicBaseUrl { get; init; } = "http://localhost/files";
}
