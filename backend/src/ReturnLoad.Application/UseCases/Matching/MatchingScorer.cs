using ReturnLoad.Domain.Loads;

namespace ReturnLoad.Application.UseCases.Matching;

/// <summary>The result of ranking one eligible load for a driver (M5).</summary>
public sealed record MatchScore(int Score, int Stars, string Reason);

/// <summary>
/// The MVP ranking stage of the matching engine (MATCHING_ENGINE.md §3). Pure and deterministic
/// so every ranking is explainable and unit-tested. Eligibility (the hard filters) has already
/// been applied — this only orders the survivors. Signals used today: pickup proximity to the
/// driver, payload utilisation, and haul length. Driver rating and Trusted-Network preference
/// are reserved as future additive bonuses (they need new data, so they are not scored yet).
/// </summary>
public static class MatchingScorer
{
    private const double EarthRadiusKm = 6371.0;

    /// <summary>
    /// Scores one eligible load (0–100) for a driver operating a vehicle of
    /// <paramref name="vehicleCapacityKg"/>. When the driver's current location is known, pickup
    /// proximity dominates; otherwise that weight is applied neutrally so ranking still works.
    /// </summary>
    public static MatchScore Score(Load load, decimal vehicleCapacityKg, double? driverLat, double? driverLng, MatchingOptions options)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(options);

        List<string> reasons = [];

        // 1) Pickup proximity — the dominant signal (Madurai driver → Madurai pickup ranks top).
        double proximityFactor;
        if (driverLat is double lat && driverLng is double lng)
        {
            double km = HaversineKm(lat, lng, load.Origin.Coordinate.Latitude, load.Origin.Coordinate.Longitude);
            proximityFactor = Clamp01(1 - (km / options.MaxPickupRadiusKm));
            reasons.Add($"pickup {km:0} km away");
        }
        else
        {
            proximityFactor = 0.5; // neutral: no location set, rank by the other signals
            reasons.Add("set your location to rank by distance");
        }

        // 2) Payload utilisation — a fuller truck is a better match (weight ≤ capacity: filtered).
        double utilisation = vehicleCapacityKg <= 0 ? 0 : Clamp01((double)(load.Requirement.Weight.Kilograms / vehicleCapacityKg));
        reasons.Add($"fills {utilisation * 100:0}% of capacity");

        // 3) Haul length — longer paid distance means more earning.
        double haulFactor = 0;
        if (load.DistanceKm is decimal distance && distance > 0)
        {
            haulFactor = Clamp01((double)distance / options.MaxHaulKm);
            reasons.Add($"{distance:0} km haul");
        }

        double raw = (proximityFactor * options.ProximityWeight)
            + (utilisation * options.UtilizationWeight)
            + (haulFactor * options.HaulWeight);

        int score = (int)Math.Round(Math.Clamp(raw, 0, 100));
        int stars = Math.Clamp((int)Math.Ceiling(score / 20.0), 1, 5);
        return new MatchScore(score, stars, string.Join(" · ", reasons));
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    /// <summary>Great-circle distance in km (approximation used for ranking, not billing).</summary>
    public static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        double dLat = ToRad(lat2 - lat1);
        double dLng = ToRad(lng2 - lng1);
        double a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2));
        return EarthRadiusKm * (2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)));
    }

    private static double ToRad(double degrees) => degrees * Math.PI / 180.0;
}
