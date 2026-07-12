using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Identity;

/// <summary>
/// Links a member (a <see cref="UserProfile"/>) to a <see cref="Carrier"/> with a
/// carrier-scoped role (§12, §13). The join that answers "who belongs to which carrier, as
/// what". A member may be re-associated over time; the rule that a driver has at most one
/// <b>active</b> carrier association is enforced across aggregates in the application layer.
/// <para><b>Invariants:</b> member, carrier, and role required; starts
/// <see cref="AssociationStatus.Pending"/>; a revoked association cannot be re-activated
/// (create a new one).</para>
/// </summary>
public sealed class Association : AggregateRoot<Guid>
{
    private Association(Guid id, Guid carrierId, Guid memberUserProfileId, AssociationRole role)
        : base(id)
    {
        CarrierId = carrierId;
        MemberUserProfileId = memberUserProfileId;
        Role = role;
        Status = AssociationStatus.Pending;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private Association()
    {
    }

    public Guid CarrierId { get; }

    public Guid MemberUserProfileId { get; }

    public AssociationRole Role { get; private set; }

    public AssociationStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static Association Create(Guid carrierId, Guid memberUserProfileId, AssociationRole role)
    {
        Guard.AgainstDefault(carrierId, "Carrier id", "association_carrier_required");
        Guard.AgainstDefault(memberUserProfileId, "Member id", "association_member_required");
        return new Association(Guid.NewGuid(), carrierId, memberUserProfileId, role);
    }

    public void Activate()
    {
        Guard.Against(Status == AssociationStatus.Revoked, "A revoked association cannot be re-activated.", "association_revoked");
        Status = AssociationStatus.Active;
    }

    public void Revoke() => Status = AssociationStatus.Revoked;
}
