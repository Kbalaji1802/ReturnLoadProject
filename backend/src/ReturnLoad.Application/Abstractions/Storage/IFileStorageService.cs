namespace ReturnLoad.Application.Abstractions.Storage;

/// <summary>
/// Provider-agnostic file storage (ADR-0012). Business code (the Documents context,
/// M6) depends only on this abstraction; the concrete backend — local disk in
/// development, later Azure Blob / AWS S3 / MinIO — is an Infrastructure concern chosen
/// with the cloud decision (03_TECHNICAL_BIBLE.md §11) and swapped without touching
/// business code. Defined in Application so the dependency points inward (ADR-0006).
/// </summary>
public interface IFileStorageService
{
    /// <summary>Stores a file and returns its durable storage key + metadata.</summary>
    Task<StoredFile> SaveAsync(FileUploadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a file's content by key, or <see langword="null"/> if it does not exist.</summary>
    Task<FileContent?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Deletes a file by key. Succeeds silently if the key is already absent.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces a temporary, time-limited URL granting direct read access to a file —
    /// so clients download large documents from storage, not through the API.
    /// </summary>
    Task<Uri> GenerateTemporaryUrlAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken = default);
}

/// <summary>A request to store a file.</summary>
/// <param name="Content">The file bytes. The caller owns the stream's lifetime.</param>
/// <param name="FileName">Original file name (used to derive an extension).</param>
/// <param name="ContentType">MIME type, e.g. <c>application/pdf</c>.</param>
public sealed record FileUploadRequest(Stream Content, string FileName, string ContentType);

/// <summary>Metadata for a stored file.</summary>
/// <param name="Key">The durable storage key used to retrieve, delete, or sign the file.</param>
/// <param name="FileName">Original file name.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="SizeBytes">Stored size in bytes.</param>
public sealed record StoredFile(string Key, string FileName, string ContentType, long SizeBytes);

/// <summary>A file's content plus the metadata needed to serve it.</summary>
/// <param name="Content">A readable stream of the file's bytes.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="FileName">Original file name.</param>
public sealed record FileContent(Stream Content, string ContentType, string FileName);
