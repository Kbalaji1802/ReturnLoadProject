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
    public void TotalPages_uses_ceiling_division(long totalRecords, int pageSize, int expectedPages)
    {
        PagedResult<string> page = PagedResult<string>.Create([], page: 1, pageSize: pageSize, totalRecords: totalRecords);

        Assert.Equal(expectedPages, page.TotalPages);
    }

    [Fact]
    public void TotalPages_is_zero_when_page_size_is_not_positive()
    {
        PagedResult<string> page = PagedResult<string>.Create([], page: 1, pageSize: 0, totalRecords: 100);

        Assert.Equal(0, page.TotalPages);
    }

    [Fact]
    public void Navigation_flags_reflect_a_middle_page()
    {
        PagedResult<int> page = PagedResult<int>.Create([4, 5, 6], page: 2, pageSize: 3, totalRecords: 9);

        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public void First_page_has_no_previous_and_last_page_has_no_next()
    {
        PagedResult<int> firstAndOnly = PagedResult<int>.Create([1], page: 1, pageSize: 10, totalRecords: 5);

        Assert.False(firstAndOnly.HasPreviousPage);
        Assert.False(firstAndOnly.HasNextPage);
    }

    [Fact]
    public void Create_carries_items_and_paging_metadata()
    {
        PagedResult<int> page = PagedResult<int>.Create([1, 2], page: 3, pageSize: 2, totalRecords: 42);

        Assert.Equal([1, 2], page.Items);
        Assert.Equal(3, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(42, page.TotalRecords);
    }
}
