using ReturnLoad.Shared.Api;

namespace ReturnLoad.UnitTests.Shared;

public sealed class ApiResponseTests
{
    [Fact]
    public void Ok_produces_a_success_envelope_with_no_errors()
    {
        ApiResponse<string> response = ApiResponse<string>.Ok("payload", "done");

        Assert.True(response.Success);
        Assert.Equal("done", response.Message);
        Assert.Equal("payload", response.Data);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public void Fail_produces_an_error_envelope_with_no_data()
    {
        ApiError error = ApiError.Validation("email", "Email format is invalid.", "INVALID_EMAIL");

        ApiResponse<object> response = ApiResponse<object>.Fail(error, "Validation failed.");

        Assert.False(response.Success);
        Assert.Null(response.Data);
        Assert.Equal("Validation failed.", response.Message);
        ApiError only = Assert.Single(response.Errors);
        Assert.Equal("email", only.Field);
        Assert.Equal("INVALID_EMAIL", only.Code);
    }

    [Fact]
    public void WithTraceId_stamps_the_correlation_id_without_mutating_the_original()
    {
        ApiResponse<string> original = ApiResponse<string>.Ok("x");

        ApiResponse<string> stamped = original.WithTraceId("abc123");

        Assert.Equal("abc123", stamped.TraceId);
        Assert.Equal(string.Empty, original.TraceId);
    }

    [Fact]
    public void General_error_has_no_field()
    {
        ApiError error = ApiError.General(ErrorCodes.NotFound, "Not found.");

        Assert.Null(error.Field);
        Assert.Equal(ErrorCodes.NotFound, error.Code);
    }
}
