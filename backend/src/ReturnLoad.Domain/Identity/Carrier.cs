using ReturnLoad.Domain.Common;
using ReturnLoad.Domain.ValueObjects;

namespace ReturnLoad.Domain.Identity;

/// <summary>
/// A carrier — the company or owner-operator that owns/operates trucks (glossary §8). Owns
/// Vehicles and has member Drivers/Dispatchers via <see cref="Association"/>s.
/// <para><b>Invariants:</b> legal name and contact required; starts <see cref="CarrierStatus.Pending"/>;
/// only an Active carrier transacts; Blocked is terminal.</para>
/// </summary>
public sealed class Carrier : AggregateRoot<Guid>
{
    private Carrier(Guid id, string legalName, MobileNumber contact, GstNumber? gst)
        : base(id)
    {
        LegalName = legalName;
        Contact = contact;
        Gst = gst;
        Status = CarrierStatus.Pending;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public string LegalName { get; private set; }

    public MobileNumber Contact { get; private set; }

    public GstNumber? Gst { get; private set; }

    public CarrierStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static Carrier Register(string legalName, MobileNumber contact, GstNumber? gst = null)
    {
        string name = Guard.AgainstNullOrWhiteSpace(legalName, "Carrier legal name", "carrier_name_required");
        ArgumentNullException.ThrowIfNull(contact);
        Carrier carrier = new(Guid.NewGuid(), name, contact, gst);
        carrier.Raise(new CarrierRegistered(carrier.Id, carrier.CreatedAtUtc));
        return carrier;
    }

    public void Activate()
    {
        Guard.Against(Status == CarrierStatus.Blocked, "A blocked carrier cannot be activated.", "carrier_blocked");
        Status = CarrierStatus.Active;
    }

    public void Suspend()
    {
        Guard.Against(Status == CarrierStatus.Blocked, "A blocked carrier cannot be suspended.", "carrier_blocked");
        Status = CarrierStatus.Suspended;
    }

    public void Block() => Status = CarrierStatus.Blocked;
}
