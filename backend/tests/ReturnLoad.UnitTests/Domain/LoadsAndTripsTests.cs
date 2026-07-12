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
    public void Trip_start_and_complete_raise_events_and_stamp_times()
    {
        Trip trip = NewTrip();
        trip.Assign();
        trip.Start();
        Assert.Equal(TripStatus.Started, trip.Status);
        Assert.NotNull(trip.StartedAtUtc);
        Assert.Contains(trip.DomainEvents, e => e is TripStarted);

        trip.MarkInTransit();
        trip.Complete();
        Assert.Equal(TripStatus.Completed, trip.Status);
        Assert.NotNull(trip.CompletedAtUtc);
        Assert.Contains(trip.DomainEvents, e => e is TripCompleted);
    }

    [Fact]
    public void Trip_cannot_start_before_assignment()
    {
        Trip trip = NewTrip();
        Assert.Throws<DomainException>(trip.Start);
    }

    [Fact]
    public void ReturnLeg_requires_distinct_endpoints()
    {
        Assert.Throws<DomainException>(() =>
            ReturnLeg.Create(Chennai(), Chennai(), TimeWindow.Create(Now, Now.AddHours(2))));
    }
}
