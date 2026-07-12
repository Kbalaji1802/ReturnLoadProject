using ReturnLoad.Shared.Api;

namespace ReturnLoad.UnitTests.Shared;

public sealed class PagedResultTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(25, 10, 3)]
    public void TotalPages_uses_ceiling_division(long totalCount, int pageSize, int expectedPages)
    {
        PagedResult<string> page = new(Items: [], Page: 1, PageSize: pageSize, TotalCount: totalCount);

        Assert.Equal(expectedPages, page.TotalPages);
    }

    [Fact]
    public void TotalPages_is_zero_when_page_size_is_not_positive()
    {
        PagedResult<string> page = new(Items: [], Page: 1, PageSize: 0, TotalCount: 100);

        Assert.Equal(0, page.TotalPages);
    }

    [Fact]
    public void Navigation_flags_reflect_a_middle_page()
    {
        PagedResult<int> page = new(Items: [4, 5, 6], Page: 2, PageSize: 3, TotalCount: 9);

        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public void First_page_has_no_previous_and_last_page_has_no_next()
    {
        PagedResult<int> firstAndOnly = new(Items: [1], Page: 1, PageSize: 10, TotalCount: 5);

        Assert.False(firstAndOnly.HasPreviousPage);
        Assert.False(firstAndOnly.HasNextPage);
    }
}
