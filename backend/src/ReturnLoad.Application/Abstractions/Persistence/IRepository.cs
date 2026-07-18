using System.Linq.Expressions;
using ReturnLoad.Domain.Common;

namespace ReturnLoad.Application.Abstractions.Persistence;

/// <summary>
/// A persistence-ignorant repository for an aggregate root — the only kind of entity that
/// is loaded and saved as a unit (ADR-0014). Defined in Application; implemented in
/// Infrastructure over EF Core. Feature milestones add aggregate-specific query methods on
/// derived interfaces; M3.5 ships the generic contract only (no business queries).
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type, keyed by <see cref="Guid"/>.</typeparam>
public interface IRepository<TAggregate>
    where TAggregate : AggregateRoot<Guid>
{
    /// <summary>Loads an aggregate by id, or <see langword="null"/> if absent (soft-deleted rows are excluded).</summary>
    Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists aggregates matching a predicate (read-only; soft-deleted rows excluded).</summary>
    Task<IReadOnlyList<TAggregate>> ListAsync(
        Expression<Func<TAggregate, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>True when any aggregate matches the predicate.</summary>
    Task<bool> ExistsAsync(Expression<Func<TAggregate, bool>> predicate, CancellationToken cancellationToken = default);

    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

    void Update(TAggregate aggregate);

    /// <summary>Marks the aggregate for deletion (persisted as a soft delete).</summary>
    void Remove(TAggregate aggregate);
}
