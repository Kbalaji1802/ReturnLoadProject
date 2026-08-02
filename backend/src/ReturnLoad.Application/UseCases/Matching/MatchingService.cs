using Microsoft.Extensions.Options;
using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Application.UseCases.Reviews;
using ReturnLoad.Domain.Fleet;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.Loads;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Application.UseCases.Matching;

/// <summary>
/// A posted load ranked for a driver (M5): the load fields plus its match <see cref="Score"/>
/// (0–100), a 1–5 <see cref="Stars"/> rating, and a human-readable <see cref="Reason"/>. Flat so
/// clients render it like a load with extra ranking fields.
/// </summary>
public sealed record ScoredLoadView(
    Guid Id, Guid ShipperId, string? OriginAddress, string? DestinationAddress,
    DateTimeOffset PickupStart, DateTimeOffset PickupEnd, CargoType CargoType,
    decimal WeightKg, decimal? OfferedPriceInr, LoadStatus Status,
    decimal? DistanceKm, int? EstimatedDurationMinutes,
    int Score, int Stars, string Reason);

/// <summary>
/// The MVP matching engine (<c>MATCHING_ENGINE.md</c>): two stages. Stage 1 applies the hard
/// filters our data supports today — driver verified (7), vehicle verified (8), vehicle type ↔
/// cargo (1), payload capacity (2). Stage 2 (M5) <b>ranks</b> the survivors by an explainable
/// score (pickup proximity, utilisation, haul length) and returns them best-first. Geo-corridor
/// / time filters (3,4,5) arrive with PostGIS. <b>Zero matches is a valid result</b>, not an error.
/// </summary>
public interface IMatchingService
{
    /// <summary>
    /// The driver's compatible loads, ranked best-first. When the driver's current location
    /// (<paramref name="driverLat"/>/<paramref name="driverLng"/>) is supplied, proximity drives
    /// the ranking; otherwise loads are ranked by the remaining signals.
    /// </summary>
    Task<Result<IReadOnlyList<ScoredLoadView>>> FindCompatibleLoadsAsync(
        Guid authUserId, double? driverLat = null, double? driverLng = null, CancellationToken cancellationToken = default);
}

internal sealed class MatchingService : IMatchingService
{
    private readonly IRepository<UserProfile> _users;
    private readonly IRepository<DriverProfile> _drivers;
    private readonly IRepository<Association> _associations;
    private readonly IRepository<Vehicle> _vehicles;
    private readonly IRepository<Load> _loads;
    private readonly IReviewService _reviews;
    private readonly MatchingOptions _options;

    public MatchingService(
        IRepository<UserProfile> users,
        IRepository<DriverProfile> drivers,
        IRepository<Association> associations,
        IRepository<Vehicle> vehicles,
        IRepository<Load> loads,
        IReviewService reviews,
        IOptions<MatchingOptions> options)
    {
        _users = users;
        _drivers = drivers;
        _associations = associations;
        _vehicles = vehicles;
        _loads = loads;
        _reviews = reviews;
        _options = options.Value;
    }

    public async Task<Result<IReadOnlyList<ScoredLoadView>>> FindCompatibleLoadsAsync(
        Guid authUserId, double? driverLat = null, double? driverLng = null, CancellationToken cancellationToken = default)
    {
        UserProfile? profile = (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();
        DriverProfile? driver = profile is null
            ? null
            : (await _drivers.ListAsync(d => d.UserProfileId == profile.Id, cancellationToken)).FirstOrDefault();

        if (profile is null || driver is null)
        {
            return Error.Validation("Register as a driver to see matching loads.");
        }

        // Hard-filter order (correction-sprint Part 2): (1) Verified Driver → (2) Verified Vehicle →
        // (3) Available Driver → (4) Compatible Vehicle → (5) Pickup Radius → then score.

        // Filter 1 — an unverified driver is eligible for nothing (Trust & Safety §1). Not an
        // error: "no matches yet" until verification completes.
        if (driver.Status != DriverStatus.Active)
        {
            return Empty();
        }

        // Filter 3 — only an Available driver receives loads (Part 3). Busy/Offline/OnLeave/
        // VehicleService drivers get nothing; a status change takes effect on this next query.
        if (driver.Availability != DriverAvailability.Available)
        {
            return Empty();
        }

        // The driver's carrier(s). Associations are created Pending and (today) not explicitly
        // activated, so we accept any non-revoked link; tightening to Active follows when
        // association activation is tied to verification.
        IReadOnlyList<Association> associations = await _associations.ListAsync(
            a => a.MemberUserProfileId == profile.Id
                && a.Role == AssociationRole.Driver
                && a.Status != AssociationStatus.Revoked,
            cancellationToken);

        HashSet<Guid> carrierIds = [.. associations.Select(a => a.CarrierId)];
        if (carrierIds.Count == 0)
        {
            return Empty();
        }

        // Filter 8 — only verified (Active) vehicles are matchable.
        IReadOnlyList<Vehicle> vehicles = await _vehicles.ListAsync(
            v => carrierIds.Contains(v.CarrierId) && v.Status == VehicleStatus.Active, cancellationToken);
        if (vehicles.Count == 0)
        {
            return Empty();
        }

        // Filter 5 (Pickup Radius) needs the driver's current location. Without it we cannot say a
        // load is within range, so — per the requirement that drivers see only loads inside the
        // pickup radius — nothing qualifies. The client should share GPS (ADR-0019) to get matches.
        if (driverLat is not double dLat || driverLng is not double dLng)
        {
            return Empty();
        }

        IReadOnlyList<Load> posted = await _loads.ListAsync(l => l.Status == LoadStatus.Posted, cancellationToken);

        Dictionary<Guid, double> shipperRatings = [];
        List<ScoredLoadView> ranked = [];
        foreach (Load load in posted)
        {
            // Filter 4 — eligible vehicles for this load (vehicle type ↔ cargo + payload capacity).
            List<Vehicle> eligible = vehicles.Where(v => MatchingRules.IsEligible(load, v)).ToList();
            if (eligible.Count == 0)
            {
                continue;
            }

            // Filter 5 — Pickup Radius (Part 2). The load's area type sets the radius (Urban 5 /
            // Suburban 10 / Highway 25 km, all config). A driver beyond it is EXCLUDED, not ranked.
            // The radius widens the longer the load goes unaccepted (RC-2 Part 3), so a pickup no
            // nearby driver wants reaches further out instead of sitting unmatched indefinitely.
            double radiusKm = _options.ResolvePickupRadiusKm(
                load.PickupAreaType, DateTimeOffset.UtcNow - load.CreatedAtUtc);
            double pickupDistanceKm = MatchingScorer.HaversineKm(
                dLat, dLng, load.Origin.Coordinate.Latitude, load.Origin.Coordinate.Longitude);
            if (pickupDistanceKm > radiusKm)
            {
                continue;
            }

            // Best-fit vehicle: the smallest capacity that still carries it (highest utilisation).
            Vehicle bestFit = eligible.OrderBy(v => v.Capacity.MaxPayload.Kilograms).First();

            // Partner reputation: the load owner's average rating (cached per shipper this call).
            if (!shipperRatings.TryGetValue(load.ShipperId, out double rating))
            {
                RatingSummary summary = await _reviews.GetSummaryAsync(load.ShipperId, cancellationToken);
                rating = summary.Count > 0 ? summary.Average : 0;
                shipperRatings[load.ShipperId] = rating;
            }

            // Stage 2: rank the survivors, normalising proximity over this load's pickup radius.
            MatchScore score = MatchingScorer.Score(
                load, bestFit.Capacity.MaxPayload.Kilograms, driverLat, driverLng, _options,
                rating > 0 ? rating : null, proximityRadiusKm: radiusKm);
            ranked.Add(Map(load, score));
        }

        // Best opportunities first (stable tie-break by pickup time so ordering is deterministic).
        IReadOnlyList<ScoredLoadView> ordered = ranked
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.PickupStart)
            .ToList();

        return Result<IReadOnlyList<ScoredLoadView>>.Success(ordered);
    }

    private static Result<IReadOnlyList<ScoredLoadView>> Empty() =>
        Result<IReadOnlyList<ScoredLoadView>>.Success([]);

    private static ScoredLoadView Map(Load l, MatchScore score) => new(
        l.Id, l.ShipperId, l.Origin.Address, l.Destination.Address,
        l.PickupWindow.Start, l.PickupWindow.End, l.Requirement.CargoType,
        l.Requirement.Weight.Kilograms, l.OfferedPrice?.Amount, l.Status,
        l.DistanceKm, l.EstimatedDurationMinutes,
        score.Score, score.Stars, score.Reason);
}
