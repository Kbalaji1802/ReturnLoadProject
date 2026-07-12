using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.ValueObjects;

/// <summary>
/// A named place: a <see cref="GeoCoordinate"/> plus an optional human-readable address.
/// Used for load pickup/delivery points and trip endpoints. Corridor/route geometry is a
/// PostGIS concern in infrastructure, not modelled here.
/// </summary>
public sealed class Location : ValueObject
{
    private Location(GeoCoordinate coordinate, string? address)
    {
        Coordinate = coordinate;
        Address = address;
    }

    public GeoCoordinate Coordinate { get; }

    public string? Address { get; }

    public static Location Create(GeoCoordinate coordinate, string? address = null)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        return new Location(coordinate, string.IsNullOrWhiteSpace(address) ? null : address.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Coordinate;
        yield return Address;
    }
}
