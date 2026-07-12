using ReturnLoad.Shared.Results;

namespace ReturnLoad.UnitTests.Shared;

public sealed class ResultTests
{
    [Fact]
    public void Success_creates_a_successful_result_with_no_error()
    {
        Result result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_creates_a_failed_result_carrying_the_error()
    {
        Error error = Error.Validation("name is required");

        Result result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Generic_success_exposes_the_value()
    {
        Result<int> result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Accessing_the_value_of_a_failure_throws()
    {
        Result<int> result = Result<int>.Failure(Error.NotFound("missing"));

        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void Implicit_conversion_from_a_value_produces_success()
    {
        Result<string> result = "chennai";

        Assert.True(result.IsSuccess);
        Assert.Equal("chennai", result.Value);
    }

    [Fact]
    public void Implicit_conversion_from_an_error_produces_failure()
    {
        Result<string> result = Error.Conflict("duplicate load");

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
    }
}
