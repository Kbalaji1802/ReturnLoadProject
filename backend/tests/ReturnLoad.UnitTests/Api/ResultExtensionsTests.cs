using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ReturnLoad.Api.Extensions;
using ReturnLoad.Shared.Api;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.UnitTests.Api;

public sealed class ResultExtensionsTests
{
    private static HttpContext NewContext() => new DefaultHttpContext();

    [Fact]
    public void Success_result_maps_to_200_success_envelope()
    {
        Result<string> result = Result<string>.Success("hello");

        IActionResult action = result.ToApiResult(NewContext(), "fetched");

        ObjectResult obj = Assert.IsType<OkObjectResult>(action);
        ApiResponse<string> body = Assert.IsType<ApiResponse<string>>(obj.Value);
        Assert.True(body.Success);
        Assert.Equal("hello", body.Data);
        Assert.Equal("fetched", body.Message);
    }

    [Theory]
    [InlineData("validation_error", StatusCodes.Status400BadRequest, ErrorCodes.ValidationError)]
    [InlineData("not_found", StatusCodes.Status404NotFound, ErrorCodes.NotFound)]
    [InlineData("conflict", StatusCodes.Status409Conflict, ErrorCodes.Conflict)]
    [InlineData("unauthorized", StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized)]
    public void Failure_result_maps_error_code_to_status_and_public_code(
        string domainCode, int expectedStatus, string expectedPublicCode)
    {
        Error error = new(domainCode, "something went wrong");
        Result<string> result = Result<string>.Failure(error);

        IActionResult action = result.ToApiResult(NewContext());

        ObjectResult obj = Assert.IsType<ObjectResult>(action);
        Assert.Equal(expectedStatus, obj.StatusCode);
        ApiResponse<object> body = Assert.IsType<ApiResponse<object>>(obj.Value);
        Assert.False(body.Success);
        Assert.Equal(expectedPublicCode, Assert.Single(body.Errors).Code);
    }

    [Fact]
    public void Unknown_error_code_defaults_to_400_never_success()
    {
        Result<string> result = Result<string>.Failure(new Error("some_new_unmapped_code", "boom"));

        IActionResult action = result.ToApiResult(NewContext());

        ObjectResult obj = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status400BadRequest, obj.StatusCode);
    }

    [Fact]
    public void Valueless_success_result_maps_to_200_with_null_data()
    {
        IActionResult action = Result.Success().ToApiResult(NewContext());

        ObjectResult obj = Assert.IsType<OkObjectResult>(action);
        ApiResponse<object?> body = Assert.IsType<ApiResponse<object?>>(obj.Value);
        Assert.True(body.Success);
        Assert.Null(body.Data);
    }
}
