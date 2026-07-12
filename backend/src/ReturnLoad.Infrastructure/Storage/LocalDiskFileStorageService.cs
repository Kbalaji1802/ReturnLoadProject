using Microsoft.Extensions.Options;
using ReturnLoad.Application.Abstractions.Storage;

namespace ReturnLoad.Infrastructure.Storage;

/// <summary>
/// Local-disk implementation of <see cref="IFileStorageService"/> — the development /
/// default backend (ADR-0012). Files are stored under a configured root directory keyed
/// by an opaque generated key. Temporary URLs are a development stand-in (base URL +
/// expiry), not signed; a cloud provider supplies real signed URLs later.
/// </summary>
public sealed class LocalDiskFileStorageService : IFileStorageService
{
    private readonly string _rootPath;
    private readonly string _publicBaseUrl;

    public LocalDiskFileStorageService(IOptions<FileStorageOptions> options)
    {
        FileStorageOptions value = options.Value;
        _rootPath = Path.IsPathRooted(value.RootPath)
            ? value.RootPath
            : Path.Combine(AppContext.BaseDirectory, value.RootPath);
        _publicBaseUrl = value.PublicBaseUrl.TrimEnd('/');

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredFile> SaveAsync(FileUploadRequest request, CancellationToken cancellationToken = default)
    {
        string extension = Path.GetExtension(request.FileName);
        string key = $"{Guid.NewGuid():N}{extension}";
        string path = ResolvePath(key);

        await using (FileStream destination = File.Create(path))
        {
            await request.Content.CopyToAsync(destination, cancellationToken);
        }

        long size = new FileInfo(path).Length;
        return new StoredFile(key, request.FileName, request.ContentType, size);
    }

    public Task<FileContent?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        string path = ResolvePath(key);
        if (!File.Exists(path))
        {
            return Task.FromResult<FileContent?>(null);
        }

        Stream content = File.OpenRead(path);
        FileContent result = new(content, "application/octet-stream", key);
        return Task.FromResult<FileContent?>(result);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        string path = ResolvePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task<Uri> GenerateTemporaryUrlAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        long expiresAt = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        return Task.FromResult(new Uri($"{_publicBaseUrl}/{key}?expires={expiresAt}"));
    }

    /// <summary>
    /// Resolves a key to an absolute path inside the root, rejecting any key that tries
    /// to escape it (path traversal) — a key is an opaque file name, never a path.
    /// </summary>
    private string ResolvePath(string key)
    {
        string fileName = Path.GetFileName(key);
        if (string.IsNullOrWhiteSpace(fileName) || fileName != key)
        {
            throw new ArgumentException("Invalid storage key.", nameof(key));
        }

        return Path.Combine(_rootPath, fileName);
    }
}
