using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.Domain.Fleet;

/// <summary>
/// A goods vehicle owned by a carrier (glossary §8; domain map §12).
/// <para><b>Domain rules / invariants:</b></para>
/// <list type="bullet">
/// <item>Registration number is required and must be unique across the platform
/// (uniqueness enforced by a persistence index — a later milestone).</item>
/// <item>Capacity must be greater than zero (guaranteed by <see cref="VehicleCapacity"/>).</item>
/// <item>A vehicle belongs to exactly one <see cref="CarrierId"/> — set at registration and immutable.</item>
/// <item>A vehicle cannot become <see cref="VehicleStatus.Active"/> (matchable) while its
/// mandatory documents are missing/expired — the caller supplies that verified-docs check
/// (matching filter 8 / Trust &amp; Safety).</item>
/// </list>
/// </summary>
public sealed class Vehicle : AggregateRoot<Guid>
{
    private Vehicle(Guid id, Guid carrierId, VehicleRegistrationNumber registration, VehicleType type, VehicleCapacity capacity)
        : base(id)
    {
        CarrierId = carrierId;
        Registration = registration;
        Type = type;
        Capacity = capacity;
        Status = VehicleStatus.Draft;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private Vehicle()
    {
    }

    public Guid CarrierId { get; }

    public VehicleRegistrationNumber Registration { get; private set; } = null!;

    public VehicleType Type { get; private set; }

    public VehicleCapacity Capacity { get; private set; } = null!;

    public VehicleStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static Vehicle Register(
        Guid carrierId,
        VehicleRegistrationNumber registration,
        VehicleType type,
        VehicleCapacity capacity)
    {
        Guard.AgainstDefault(carrierId, "Carrier id", "vehicle_carrier_required");
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(capacity);

        Vehicle vehicle = new(Guid.NewGuid(), carrierId, registration, type, capacity);
        vehicle.Raise(new VehicleRegistered(vehicle.Id, carrierId, vehicle.CreatedAtUtc));
        return vehicle;
    }

    /// <summary>
    /// Activates the vehicle for matching. <paramref name="mandatoryDocumentsValid"/> is the
    /// application layer's verdict that RC + insurance + permit are Verified and unexpired
    /// (the cross-aggregate check the domain refuses to bypass).
    /// </summary>
    public void Activate(bool mandatoryDocumentsValid)
    {
        Guard.Against(Status == VehicleStatus.Suspended, "A suspended vehicle cannot be activated.", "vehicle_suspended");
        Guard.Against(
            !mandatoryDocumentsValid,
            "A vehicle cannot be activated while mandatory documents are missing or expired.",
            "vehicle_documents_invalid");
        Status = VehicleStatus.Active;
    }

    public void SendToMaintenance() => Status = VehicleStatus.Maintenance;

    public void Suspend() => Status = VehicleStatus.Suspended;

    public void UpdateCapacity(VehicleCapacity capacity)
    {
        ArgumentNullException.ThrowIfNull(capacity);
        Capacity = capacity;
    }
}
