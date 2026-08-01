using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.Loads;
using ReturnLoad.Domain.Trips;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.UnitTests.Domain;

public sealed class LoadsAndTripsTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);

    private static Location Chennai() => Location.Create(GeoCoordinate.Create(13.0827, 80.2707), "Chennai");

    private static Location Coimbatore() => Location.Create(GeoCoordinate.Create(11.0168, 76.9558), "Coimbatore");

    private static LoadRequirement Requirement() => LoadRequirement.Create(CargoType.General, Weight.FromTonnes(5m));

    private static Load NewLoad() =>
        Load.Create(Guid.NewGuid(), Chennai(), Coimbatore(), TimeWindow.Create(Now, Now.AddHours(6)), Requirement(), Money.Of(15000m));

    private static Trip NewTrip() =>
        Trip.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Chennai(), Coimbatore(),
            ReturnLeg.Create(Coimbatore(), Chennai(), TimeWindow.Create(Now.AddHours(8), Now.AddHours(16))));

    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromMinutes(15);

    /// <summary>Advance as the driver at a fixed time (drives the driving/physical steps).</summary>
    private static void Drive(Trip trip, TripStatus target) =>
        trip.Advance(target, TripActor.Driver, DateTimeOffset.UtcNow, ConfirmWindow);

    /// <summary>Advance as the load owner (confirms a gate).</summary>
    private static void OwnerConfirm(Trip trip, TripStatus target) =>
        trip.Advance(target, TripActor.Owner, DateTimeOffset.UtcNow, ConfirmWindow);

    [Fact]
    public void Load_create_raises_event_and_starts_draft()
    {
        Load load = NewLoad();
        Assert.Equal(LoadStatus.Draft, load.Status);
        Assert.Contains(load.DomainEvents, e => e is LoadCreated);
    }

    [Fact]
    public void Load_rejects_same_origin_and_destination()
    {
        Assert.Throws<DomainException>(() =>
            Load.Create(Guid.NewGuid(), Chennai(), Chennai(), TimeWindow.Create(Now, Now.AddHours(6)), Requirement()));
    }

    [Fact]
    public void Load_lifecycle_enforces_legal_transitions()
    {
        Load load = NewLoad();
        Assert.Throws<DomainException>(load.Book); // cannot book a draft

        load.Post();
        Assert.Contains(load.DomainEvents, e => e is LoadPosted);
        load.MarkMatched();
        load.Book();
        load.StartTransit();
        load.Deliver();
        Assert.Equal(LoadStatus.Delivered, load.Status);
        Assert.Throws<DomainException>(load.Cancel); // delivered cannot cancel
    }

    [Fact]
    public void Trip_advances_through_the_lifecycle_with_owner_confirmation_gates()
    {
        Trip trip = NewTrip();
        Drive(trip, TripStatus.DriverAccepted);
        Drive(trip, TripStatus.DriverEnRoute);
        Assert.Equal(TripStatus.DriverEnRoute, trip.Status);
        Assert.NotNull(trip.StartedAtUtc);
        Assert.Contains(trip.DomainEvents, e => e is TripStarted);

        Drive(trip, TripStatus.ArrivedPickup);
        OwnerConfirm(trip, TripStatus.PickupConfirmed); // owner gate before loading
        Drive(trip, TripStatus.Loaded);
        Drive(trip, TripStatus.InTransit);
        Drive(trip, TripStatus.ArrivedDestination);
        Drive(trip, TripStatus.Unloaded);
        OwnerConfirm(trip, TripStatus.DeliveryConfirmed); // owner gate before completion
        Drive(trip, TripStatus.Completed);

        Assert.Equal(TripStatus.Completed, trip.Status);
        Assert.NotNull(trip.CompletedAtUtc);
        Assert.False(trip.PickupAutoConfirmed);   // the owner confirmed, not a timeout
        Assert.False(trip.DeliveryAutoConfirmed);
        Assert.Contains(trip.DomainEvents, e => e is TripCompleted);
    }

    [Fact]
    public void Owner_confirmation_gate_blocks_the_driver_until_the_window_elapses()
    {
        Trip trip = NewTrip();
        Drive(trip, TripStatus.DriverAccepted);
        Drive(trip, TripStatus.DriverEnRoute);
        DateTimeOffset arrival = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);
        trip.Advance(TripStatus.ArrivedPickup, TripActor.Driver, arrival, ConfirmWindow);

        // The driver cannot self-confirm the owner gate immediately.
        Assert.Throws<DomainException>(() =>
            trip.Advance(TripStatus.PickupConfirmed, TripActor.Driver, arrival.AddMinutes(5), ConfirmWindow));

        // After the window elapses the driver may proceed — recorded as auto-confirmed (no stranding).
        trip.Advance(TripStatus.PickupConfirmed, TripActor.Driver, arrival.AddMinutes(20), ConfirmWindow);
        Assert.Equal(TripStatus.PickupConfirmed, trip.Status);
        Assert.True(trip.PickupAutoConfirmed);
    }

    [Fact]
    public void Owner_cannot_advance_a_driving_step()
    {
        Trip trip = NewTrip();
        // A driving step (DriverAccepted) may not be advanced by the owner.
        Assert.Throws<DomainException>(() =>
            trip.Advance(TripStatus.DriverAccepted, TripActor.Owner, DateTimeOffset.UtcNow, ConfirmWindow));
    }

    [Fact]
    public void Trip_cannot_skip_lifecycle_steps()
    {
        Trip trip = NewTrip();
        // Jumping straight from Created to InTransit is illegal — one step at a time.
        Assert.Throws<DomainException>(() => Drive(trip, TripStatus.InTransit));
    }

    [Fact]
    public void Trip_can_be_cancelled_before_completion()
    {
        Trip trip = NewTrip();
        Drive(trip, TripStatus.DriverAccepted);
        Drive(trip, TripStatus.Cancelled);
        Assert.Equal(TripStatus.Cancelled, trip.Status);
    }

    [Fact]
    public void ReturnLeg_requires_distinct_endpoints()
    {
        Assert.Throws<DomainException>(() =>
            ReturnLeg.Create(Chennai(), Chennai(), TimeWindow.Create(Now, Now.AddHours(2))));
    }
}
