using ReturnLoad.Shared.Api;
using ReturnLoad.Shared.Configuration;

namespace ReturnLoad.UnitTests.Shared;

public sealed class FileUploadValidatorTests
{
    private static readonly FileUploadOptions Options = new(); // 10 MB; pdf/jpeg/png

    [Fact]
    public void Accepts_a_valid_pdf()
    {
        IReadOnlyList<ApiError> errors = FileUploadValidator.Validate("permit.pdf", "application/pdf", 2048, Options);

        Assert.Empty(errors);
    }

    [Fact]
    public void Rejects_an_empty_file()
    {
        IReadOnlyList<ApiError> errors = FileUploadValidator.Validate("permit.pdf", "application/pdf", 0, Options);

        Assert.Contains(errors, e => e.Message.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_a_file_over_the_size_limit()
    {
        IReadOnlyList<ApiError> errors = FileUploadValidator.Validate(
            "permit.pdf", "application/pdf", Options.MaxFileSizeBytes + 1, Options);

        Assert.Contains(errors, e => e.Code == ErrorCodes.PayloadTooLarge);
    }

    [Fact]
    public void Rejects_a_disallowed_mime_type()
    {
        IReadOnlyList<ApiError> errors = FileUploadValidator.Validate("permit.pdf", "text/plain", 100, Options);

        Assert.Contains(errors, e => e.Message.Contains("content type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_a_disallowed_extension()
    {
        IReadOnlyList<ApiError> errors = FileUploadValidator.Validate("permit.exe", "application/pdf", 100, Options);

        Assert.Contains(errors, e => e.Message.Contains("extension", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Mime_and_extension_checks_are_case_insensitive()
    {
        IReadOnlyList<ApiError> errors = FileUploadValidator.Validate("PHOTO.JPG", "IMAGE/JPEG", 100, Options);

        Assert.Empty(errors);
    }
}
