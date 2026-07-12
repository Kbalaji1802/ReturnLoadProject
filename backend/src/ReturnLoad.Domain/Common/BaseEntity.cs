namespace ReturnLoad.Domain.Common;

/// <summary>
/// Base class for domain entities, providing a strongly-typed identity and
/// value-based equality on that identity. Aggregate roots extend
/// <see cref="AggregateRoot{TId}"/> (which adds domain events); the business entities
/// (Carrier, DriverProfile, Vehicle, Load, Trip, …) live in their bounded-context
/// namespaces under <c>ReturnLoad.Domain</c> (see 03_TECHNICAL_BIBLE.md §12).
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
public abstract class BaseEntity<TId>
    where TId : notnull
{
    protected BaseEntity(TId id) => Id = id;

    /// <summary>Parameterless constructor for the persistence layer (EF Core materialization) only.</summary>
    protected BaseEntity()
    {
    }

    public TId Id { get; protected set; } = default!;

    public override bool Equals(object? obj) =>
        obj is BaseEntity<TId> other
        && other.GetType() == GetType()
        && EqualityComparer<TId>.Default.Equals(other.Id, Id);

    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id);
}
