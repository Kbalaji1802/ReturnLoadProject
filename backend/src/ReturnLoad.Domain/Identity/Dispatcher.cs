using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Identity;

/// <summary>
/// A dispatcher — a person (via <see cref="UserProfile"/>) who assigns drivers/trucks to
/// trips for a specific <see cref="Carrier"/> (role §13). Membership itself is expressed by
/// an <see cref="Association"/>; this aggregate carries dispatcher-specific state.
/// <para><b>Invariants:</b> bound to exactly one user profile and one carrier.</para>
/// </summary>
public sealed class Dispatcher : AggregateRoot<Guid>
{
    private Dispatcher(Guid id, Guid userProfileId, Guid carrierId)
        : base(id)
    {
        UserProfileId = userProfileId;
        CarrierId = carrierId;
        IsActive = true;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private Dispatcher()
    {
    }

    public Guid UserProfileId { get; }

    public Guid CarrierId { get; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static Dispatcher Create(Guid userProfileId, Guid carrierId)
    {
        Guard.AgainstDefault(userProfileId, "User profile id", "dispatcher_user_required");
        Guard.AgainstDefault(carrierId, "Carrier id", "dispatcher_carrier_required");
        return new Dispatcher(Guid.NewGuid(), userProfileId, carrierId);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
