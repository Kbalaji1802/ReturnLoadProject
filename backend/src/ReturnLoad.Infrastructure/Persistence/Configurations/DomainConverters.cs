using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.Infrastructure.Persistence.Configurations;

/// <summary>Reusable EF value converters for domain value objects stored in a single column.</summary>
internal static class DomainConverters
{
    /// <summary>
    /// GeoCoordinate ↔ <c>"lat;lng"</c>. A single-column form; true geo-proximity indexing is
    /// a PostGIS concern for the geo/matching milestone (03_TECHNICAL_BIBLE.md §5).
    /// </summary>
    public static readonly ValueConverter<GeoCoordinate, string> GeoCoordinate = new(
        coordinate => coordinate.Latitude.ToString(CultureInfo.InvariantCulture) + ";" + coordinate.Longitude.ToString(CultureInfo.InvariantCulture),
        value => FromString(value));

    private static GeoCoordinate FromString(string value)
    {
        string[] parts = value.Split(';');
        return Domain.ValueObjects.GeoCoordinate.Create(
            double.Parse(parts[0], CultureInfo.InvariantCulture),
            double.Parse(parts[1], CultureInfo.InvariantCulture));
    }
}
