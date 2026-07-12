namespace ReturnLoad.Domain.Common;

/// <summary>
/// Base class for domain entities, providing a strongly-typed identity and
/// value-based equality on that identity.
/// <para>
/// Concrete business entities (Load, Trip, Carrier, Vehicle, …) are intentionally
/// NOT defined here yet — they are introduced by the domain-model task T-002
/// (see 03_TECHNICAL_BIBLE.md §12). This type only establishes the shared shape
/// they will inherit.
/// </para>
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
public abstract class BaseEntity<TId>
    where TId : notnull
{
    protected BaseEntity(TId id) => Id = id;

    public TId Id { get; protected set; }

    public override bool Equals(object? obj) =>
        obj is BaseEntity<TId> other
        && other.GetType() == GetType()
        && EqualityComparer<TId>.Default.Equals(other.Id, Id);

    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id);
}
