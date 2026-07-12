namespace ReturnLoad.Domain.Common;

/// <summary>
/// Base class for aggregate roots — the only entities loaded, saved, and referenced as a
/// unit, and the sole entry points through which their internals are modified (so
/// invariants stay enforced). Records <see cref="IDomainEvent"/>s that occurred while
/// handling a command; the application layer collects and dispatches them after the
/// change is persisted, then calls <see cref="ClearDomainEvents"/>.
/// </summary>
/// <typeparam name="TId">The type of the aggregate's identifier.</typeparam>
public abstract class AggregateRoot<TId> : BaseEntity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    /// <summary>Events raised by this aggregate since it was loaded, in order.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
