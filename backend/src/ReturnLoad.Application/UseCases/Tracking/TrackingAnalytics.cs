using ReturnLoad.Domain.Tracking;

namespace ReturnLoad.Application.UseCases.Tracking;

/// <summary>Trip analytics derived from its tracking breadcrumb (M6).</summary>
public sealed record TripTrackingSummary(
    double DistanceKm, int DurationMinutes, double AvgSpeedKph, double MaxSpeedKph, int IdleMinutes, int PointCount);

/// <summary>
/// Pure, deterministic trip analytics over an ordered tracking breadcrumb (M6 "Analytics"):
/// distance travelled, duration, average/maximum speed, and idle time. Kept side-effect free so
/// it is unit-tested with fixtures and can later feed AI (fuel, driver-behaviour scoring).
/// </summary>
public static class TrackingAnalytics
{
    private const double EarthRadiusKm = 6371.0;
    private const double IdleSpeedThresholdKph = 3.0;

    /// <summary>Summarises points that are already ordered by capture time (oldest first).</summary>
    public static TripTrackingSummary Summarize(IReadOnlyList<TrackingEvent> orderedPoints)
    {
        ArgumentNullException.ThrowIfNull(orderedPoints);
        if (orderedPoints.Count == 0)
        {
            return new TripTrackingSummary(0, 0, 0, 0, 0, 0);
        }

        double distanceKm = 0;
        double maxSpeed = 0;
        double idleMinutes = 0;

        for (int i = 0; i < orderedPoints.Count; i++)
        {
            double? speed = orderedPoints[i].Point.SpeedKph;
            if (speed is double s && s > maxSpeed)
            {
                maxSpeed = s;
            }

            if (i == 0)
            {
                continue;
            }

            TrackingEvent prev = orderedPoints[i - 1];
            TrackingEvent curr = orderedPoints[i];
            distanceKm += HaversineKm(
                prev.Point.Coordinate.Latitude, prev.Point.Coordinate.Longitude,
                curr.Point.Coordinate.Latitude, curr.Point.Coordinate.Longitude);

            double segmentMinutes = (curr.CapturedAtUtc - prev.CapturedAtUtc).TotalMinutes;
            if (segmentMinutes > 0)
            {
                double segmentSpeed = distanceKm > 0 ? (HaversineKm(
                    prev.Point.Coordinate.Latitude, prev.Point.Coordinate.Longitude,
                    curr.Point.Coordinate.Latitude, curr.Point.Coordinate.Longitude) / (segmentMinutes / 60.0)) : 0;
                if (segmentSpeed < IdleSpeedThresholdKph)
                {
                    idleMinutes += segmentMinutes;
                }
            }
        }

        double durationMinutes = (orderedPoints[^1].CapturedAtUtc - orderedPoints[0].CapturedAtUtc).TotalMinutes;
        double avgSpeed = durationMinutes > 0 ? distanceKm / (durationMinutes / 60.0) : 0;

        return new TripTrackingSummary(
            Math.Round(distanceKm, 2),
            (int)Math.Round(durationMinutes),
            Math.Round(avgSpeed, 1),
            Math.Round(maxSpeed, 1),
            (int)Math.Round(idleMinutes),
            orderedPoints.Count);
    }

    /// <summary>
    /// A robust "current" speed (kph) for ETA (Part 6): the average of the most recent points'
    /// reported device speeds (last <paramref name="window"/>), ignoring idle/zero readings; falls
    /// back to the overall travel average. Avoids ETA collapsing to null when the trip is briefly
    /// idle (e.g. stopped at the start), which the whole-trip average did.
    /// </summary>
    public static double RecentSpeedKph(IReadOnlyList<TrackingEvent> orderedPoints, int window = 5)
    {
        ArgumentNullException.ThrowIfNull(orderedPoints);
        if (orderedPoints.Count == 0)
        {
            return 0;
        }

        List<double> recentMoving = orderedPoints
            .Skip(Math.Max(0, orderedPoints.Count - window))
            .Select(p => p.Point.SpeedKph)
            .Where(s => s is double v && v > IdleSpeedThresholdKph)
            .Select(s => s!.Value)
            .ToList();

        return recentMoving.Count > 0
            ? Math.Round(recentMoving.Average(), 1)
            : Summarize(orderedPoints).AvgSpeedKph;
    }

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
