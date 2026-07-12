using System.Text;
using Microsoft.Extensions.Options;
using ReturnLoad.Application.Abstractions.Storage;
using ReturnLoad.Infrastructure.Storage;

namespace ReturnLoad.UnitTests.Infrastructure;

public sealed class LocalDiskFileStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "returnload-tests", Guid.NewGuid().ToString("N"));
    private readonly LocalDiskFileStorageService _storage;

    public LocalDiskFileStorageServiceTests()
    {
        FileStorageOptions options = new() { RootPath = _root, PublicBaseUrl = "http://files.test" };
        _storage = new LocalDiskFileStorageService(Options.Create(options));
    }

    [Fact]
    public async Task Save_then_get_round_trips_the_content()
    {
        StoredFile stored = await SaveAsync("hello world", "note.pdf", "application/pdf");

        Assert.Equal("note.pdf", stored.FileName);
        Assert.Equal(11, stored.SizeBytes);

        FileContent? fetched = await _storage.GetAsync(stored.Key);
        Assert.NotNull(fetched);
        using StreamReader reader = new(fetched!.Content);
        Assert.Equal("hello world", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Get_returns_null_for_a_missing_key()
    {
        Assert.Null(await _storage.GetAsync("does-not-exist.pdf"));
    }

    [Fact]
    public async Task Delete_removes_the_file()
    {
        StoredFile stored = await SaveAsync("data", "x.pdf", "application/pdf");

        await _storage.DeleteAsync(stored.Key);

        Assert.Null(await _storage.GetAsync(stored.Key));
    }

    [Fact]
    public async Task Temporary_url_includes_the_key_and_an_expiry()
    {
        StoredFile stored = await SaveAsync("data", "x.pdf", "application/pdf");

        Uri url = await _storage.GenerateTemporaryUrlAsync(stored.Key, TimeSpan.FromMinutes(5));

        Assert.Contains(stored.Key, url.ToString());
        Assert.Contains("expires=", url.ToString());
    }

    [Fact]
    public async Task A_key_that_escapes_the_root_is_rejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.GetAsync("../escape.pdf"));
    }

    private async Task<StoredFile> SaveAsync(string content, string fileName, string contentType)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(content));
        return await _storage.SaveAsync(new FileUploadRequest(stream, fileName, contentType));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
