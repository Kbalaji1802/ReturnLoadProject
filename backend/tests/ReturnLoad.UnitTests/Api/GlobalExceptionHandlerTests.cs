using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ReturnLoad.Api.Middleware;
using ReturnLoad.Shared.Api;

namespace ReturnLoad.UnitTests.Api;

public sealed class GlobalExceptionHandlerTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static async Task<(int Status, ApiResponse<JsonElement> Body)> HandleAsync(Exception exception)
    {
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();

        GlobalExceptionHandler handler = new(NullLogger<GlobalExceptionHandler>.Instance);
        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        ApiResponse<JsonElement> body =
            (await JsonSerializer.DeserializeAsync<ApiResponse<JsonElement>>(context.Response.Body, Web))!;

        return (context.Response.StatusCode, body);
    }

    [Fact]
    public async Task Validation_exception_maps_to_400_with_per_field_errors()
    {
        ValidationException exception = new(
        [
            new ValidationFailure("Email", "Email format is invalid.") { ErrorCode = "INVALID_EMAIL" },
            new ValidationFailure("Age", "Age must be positive."),
        ]);

        (int status, ApiResponse<JsonElement> body) = await HandleAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.False(body.Success);
        Assert.Equal("Validation failed.", body.Message);
        Assert.Equal(2, body.Errors.Count);
        Assert.Contains(body.Errors, e => e.Field == "Email" && e.Code == "INVALID_EMAIL");
        Assert.Contains(body.Errors, e => e.Field == "Age" && e.Code == ErrorCodes.ValidationError);
        Assert.False(string.IsNullOrEmpty(body.TraceId));
    }

    [Fact]
    public async Task Unexpected_exception_maps_to_500_internal_error_without_leaking_details()
    {
        (int status, ApiResponse<JsonElement> body) = await HandleAsync(new InvalidOperationException("secret db string"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.False(body.Success);
        ApiError only = Assert.Single(body.Errors);
        Assert.Equal(ErrorCodes.InternalError, only.Code);
        Assert.DoesNotContain("secret db string", body.Message);
        Assert.DoesNotContain("secret db string", only.Message);
    }
}
