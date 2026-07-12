using ReturnLoad.Shared.Api;

namespace ReturnLoad.UnitTests.Shared;

public sealed class PaginationQueryTests
{
    [Fact]
    public void Defaults_are_page_one_and_default_page_size()
    {
        PaginationQuery query = new();

        Assert.Equal(1, query.Page);
        Assert.Equal(PaginationQuery.DefaultPageSize, query.PageSize);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void Page_is_normalised_to_at_least_one(int requested, int expected)
    {
        PaginationQuery query = new() { Page = requested };

        Assert.Equal(expected, query.Page);
    }

    [Theory]
    [InlineData(0, PaginationQuery.DefaultPageSize)] // 0 -> default
    [InlineData(50, 50)]
    [InlineData(1000, PaginationQuery.MaxPageSize)]  // clamped to the hard cap
    public void PageSize_is_clamped_within_bounds(int requested, int expected)
    {
        PaginationQuery query = new() { PageSize = requested };

        Assert.Equal(expected, query.PageSize);
    }

    [Fact]
    public void Skip_and_take_derive_from_page_and_size()
    {
        PaginationQuery query = new() { Page = 3, PageSize = 20 };

        Assert.Equal(40, query.Skip);
        Assert.Equal(20, query.Take);
    }
}
