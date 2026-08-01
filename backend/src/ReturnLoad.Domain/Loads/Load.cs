using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.Domain.Loads;

/// <summary>
/// A shipment a shipper needs moved: cargo + origin + destination + pickup window +
/// requirements (glossary §8). The unit the matching engine pairs with a truck's return leg.
/// <para><b>Domain rules / invariants:</b></para>
/// <list type="bullet">
/// <item>Shipper, origin, destination, pickup window, and requirement are required.</item>
/// <item>Origin and destination must differ.</item>
/// <item>An optional offered price must be non-negative (via <see cref="Money"/>).</item>
/// <item>Legal lifecycle transitions only (Draft → Posted → Matched → Booked → InTransit →
/// Delivered; Cancellable before delivery).</item>
/// </list>
/// </summary>
public sealed class Load : AggregateRoot<Guid>
{
    private Load(
        Guid id,
        Guid shipperId,
        Location origin,
        Location destination,
        TimeWindow pickupWindow,
        LoadRequirement requirement,
        Money? offeredPrice,
        AreaType pickupAreaType)
        : base(id)
    {
        ShipperId = shipperId;
        Origin = origin;
        Destination = destination;
        PickupWindow = pickupWindow;
        Requirement = requirement;
        OfferedPrice = offeredPrice;
        PickupAreaType = pickupAreaType;
        Status = LoadStatus.Draft;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private Load()
    {
    }

    public Guid ShipperId { get; }

    public Location Origin { get; private set; } = null!;

    public Location Destination { get; private set; } = null!;

    public TimeWindow PickupWindow { get; private set; } = null!;

    public LoadRequirement Requirement { get; private set; } = null!;

    public Money? OfferedPrice { get; private set; }

    /// <summary>
    /// The pickup's road environment (Part 2). Sets the matching pickup radius (Urban/Suburban/
    /// Highway → configured km). Chosen by the shipper at posting; defaults to Suburban.
    /// </summary>
    public AreaType PickupAreaType { get; private set; }

    public LoadStatus Status { get; private set; }

    /// <summary>Road distance origin→destination in km, computed by the platform (M4.3 Step 1).</summary>
    public decimal? DistanceKm { get; private set; }

    /// <summary>Estimated drive time origin→destination in minutes, computed by the platform.</summary>
    public int? EstimatedDurationMinutes { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static Load Create(
        Guid shipperId,
        Location origin,
        Location destination,
        TimeWindow pickupWindow,
        LoadRequirement requirement,
        Money? offeredPrice = null,
        AreaType pickupAreaType = AreaType.Suburban)
    {
        Guard.AgainstDefault(shipperId, "Shipper id", "load_shipper_required");
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(pickupWindow);
        ArgumentNullException.ThrowIfNull(requirement);
        Guard.Against(origin == destination, "Origin and destination must differ.", "load_same_origin_destination");

        Load load = new(Guid.NewGuid(), shipperId, origin, destination, pickupWindow, requirement, offeredPrice, pickupAreaType);
        load.Raise(new LoadCreated(load.Id, shipperId, load.CreatedAtUtc));
        return load;
    }

    /// <summary>
    /// Records the platform-computed road distance + estimated duration for this load
    /// (M4.3 Step 1). Derived from a route provider at posting; the shipper never enters it.
    /// </summary>
    public void SetRoute(decimal distanceKm, int estimatedDurationMinutes)
    {
        Guard.AgainstNegative(distanceKm, "Distance", "load_distance_negative");
        Guard.Against(estimatedDurationMinutes < 0, "Estimated duration cannot be negative.", "load_duration_negative");
        DistanceKm = distanceKm;
        EstimatedDurationMinutes = estimatedDurationMinutes;
    }

    public void Post()
    {
        Guard.Against(Status != LoadStatus.Draft, "Only a draft load can be posted.", "load_not_draft");
        Status = LoadStatus.Posted;
        Raise(new LoadPosted(Id, DateTimeOffset.UtcNow));
    }

    public void MarkMatched()
    {
        Guard.Against(Status != LoadStatus.Posted, "Only a posted load can be matched.", "load_not_posted");
        Status = LoadStatus.Matched;
    }

    public void Book()
    {
        Guard.Against(Status != LoadStatus.Matched, "Only a matched load can be booked.", "load_not_matched");
        Status = LoadStatus.Booked;
    }

    public void StartTransit()
    {
        Guard.Against(Status != LoadStatus.Booked, "Only a booked load can start transit.", "load_not_booked");
        Status = LoadStatus.InTransit;
    }

    public void Deliver()
    {
        Guard.Against(Status != LoadStatus.InTransit, "Only an in-transit load can be delivered.", "load_not_in_transit");
        Status = LoadStatus.Delivered;
    }

    public void Cancel()
    {
        Guard.Against(
            Status is LoadStatus.Delivered or LoadStatus.Cancelled,
            "A delivered or already-cancelled load cannot be cancelled.",
            "load_not_cancellable");
        Status = LoadStatus.Cancelled;
    }
}
