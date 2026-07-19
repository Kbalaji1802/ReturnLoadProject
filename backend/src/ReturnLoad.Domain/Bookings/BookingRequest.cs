using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Bookings;

/// <summary>
/// A driver's request to carry a specific load with a specific vehicle (M4.3 Steps 3–4). This
/// is the freight-marketplace "booking request" pattern: several drivers may request the same
/// load; the load owner chooses one. Accepting produces a Trip; the others are rejected.
/// <para><b>Invariants:</b> load, driver, carrier, and vehicle are required; starts
/// <see cref="BookingRequestStatus.Pending"/>; only a Pending request can be accepted,
/// rejected, or withdrawn (a decision is final).</para>
/// </summary>
public sealed class BookingRequest : AggregateRoot<Guid>
{
    private BookingRequest(Guid id, Guid loadId, Guid driverProfileId, Guid carrierId, Guid vehicleId)
        : base(id)
    {
        LoadId = loadId;
        DriverProfileId = driverProfileId;
        CarrierId = carrierId;
        VehicleId = vehicleId;
        Status = BookingRequestStatus.Pending;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private BookingRequest()
    {
    }

    public Guid LoadId { get; }

    public Guid DriverProfileId { get; }

    public Guid CarrierId { get; }

    public Guid VehicleId { get; }

    public BookingRequestStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public static BookingRequest Create(Guid loadId, Guid driverProfileId, Guid carrierId, Guid vehicleId)
    {
        Guard.AgainstDefault(loadId, "Load id", "booking_load_required");
        Guard.AgainstDefault(driverProfileId, "Driver id", "booking_driver_required");
        Guard.AgainstDefault(carrierId, "Carrier id", "booking_carrier_required");
        Guard.AgainstDefault(vehicleId, "Vehicle id", "booking_vehicle_required");

        BookingRequest request = new(Guid.NewGuid(), loadId, driverProfileId, carrierId, vehicleId);
        request.Raise(new BookingRequested(request.Id, loadId, driverProfileId, request.CreatedAtUtc));
        return request;
    }

    public void Accept()
    {
        Guard.Against(Status != BookingRequestStatus.Pending, "Only a pending request can be accepted.", "booking_not_pending");
        Status = BookingRequestStatus.Accepted;
        DecidedAtUtc = DateTimeOffset.UtcNow;
        Raise(new BookingAccepted(Id, LoadId, DriverProfileId, DecidedAtUtc.Value));
    }

    public void Reject()
    {
        Guard.Against(Status != BookingRequestStatus.Pending, "Only a pending request can be rejected.", "booking_not_pending");
        Status = BookingRequestStatus.Rejected;
        DecidedAtUtc = DateTimeOffset.UtcNow;
        Raise(new BookingRejected(Id, LoadId, DecidedAtUtc.Value));
    }

    public void Withdraw()
    {
        Guard.Against(Status != BookingRequestStatus.Pending, "Only a pending request can be withdrawn.", "booking_not_pending");
        Status = BookingRequestStatus.Withdrawn;
        DecidedAtUtc = DateTimeOffset.UtcNow;
    }
}
