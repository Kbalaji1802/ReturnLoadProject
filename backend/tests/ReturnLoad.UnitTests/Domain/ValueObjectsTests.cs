using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.UnitTests.Domain;

public sealed class ValueObjectsTests
{
    [Theory]
    [InlineData("9876543210", "+919876543210")]
    [InlineData("+91 98765 43210", "+919876543210")]
    [InlineData("09876543210", "+919876543210")]
    public void MobileNumber_normalises_valid_indian_numbers(string input, string expected)
    {
        Assert.Equal(expected, MobileNumber.Create(input).Value);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567890")]  // starts with 1 — invalid Indian mobile
    [InlineData("")]
    public void MobileNumber_rejects_invalid_numbers(string input)
    {
        Assert.Throws<DomainException>(() => MobileNumber.Create(input));
    }

    [Fact]
    public void EmailAddress_is_lowercased_and_validated()
    {
        Assert.Equal("driver@returnload.test", EmailAddress.Create("Driver@ReturnLoad.test").Value);
        Assert.Throws<DomainException>(() => EmailAddress.Create("not-an-email"));
    }

    [Fact]
    public void Money_rejects_negative_and_blocks_cross_currency_addition()
    {
        Assert.Throws<DomainException>(() => Money.Of(-1m));
        Money inr = Money.Of(100m);
        Assert.Equal("INR", inr.Currency);
        Assert.Throws<DomainException>(() => inr.Add(Money.Of(5m, "USD")));
        Assert.Equal(150m, inr.Add(Money.Of(50m)).Amount);
    }

    [Fact]
    public void Weight_must_be_positive_and_compares()
    {
        Assert.Throws<DomainException>(() => Weight.FromKilograms(0m));
        Assert.True(Weight.FromKilograms(1000m).IsGreaterThanOrEqualTo(Weight.FromKilograms(999m)));
    }

    [Fact]
    public void Distance_rejects_negative_but_allows_zero()
    {
        Assert.Equal(0m, Distance.Zero.Kilometres);
        Assert.Throws<DomainException>(() => Distance.FromKilometres(-1m));
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(0, 181)]
    [InlineData(-91, 0)]
    public void GeoCoordinate_rejects_out_of_range(double lat, double lon)
    {
        Assert.Throws<DomainException>(() => GeoCoordinate.Create(lat, lon));
    }

    [Fact]
    public void TimeWindow_requires_end_after_start_and_detects_overlap()
    {
        DateTimeOffset now = new(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);
        Assert.Throws<DomainException>(() => TimeWindow.Create(now, now));

        TimeWindow a = TimeWindow.Create(now, now.AddHours(4));
        TimeWindow b = TimeWindow.Create(now.AddHours(2), now.AddHours(6));
        TimeWindow c = TimeWindow.Create(now.AddHours(5), now.AddHours(7));
        Assert.True(a.Overlaps(b));
        Assert.False(a.Overlaps(c));
    }

    [Fact]
    public void Value_objects_are_equal_by_value()
    {
        Assert.Equal(Money.Of(100m), Money.Of(100m));
        Assert.Equal(GeoCoordinate.Create(13.08, 80.27), GeoCoordinate.Create(13.08, 80.27));
        Assert.NotEqual(Weight.FromKilograms(10m), Weight.FromKilograms(11m));
    }
}
