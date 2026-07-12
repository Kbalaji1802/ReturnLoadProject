using Microsoft.EntityFrameworkCore;
using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Domain.Common;

namespace ReturnLoad.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IRepository{TAggregate}"/>. Registered as an open
/// generic so any aggregate gets a repository for free. Saving is done via
/// <see cref="IUnitOfWork"/> (the DbContext), not per-call, so a use case commits atomically.
/// </summary>
internal sealed class EfRepository<TAggregate> : IRepository<TAggregate>
    where TAggregate : AggregateRoot<Guid>
{
    private readonly ApplicationDbContext _db;

    public EfRepository(ApplicationDbContext db) => _db = db;

    public async Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Set<TAggregate>().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default) =>
        await _db.Set<TAggregate>().AddAsync(aggregate, cancellationToken);

    public void Update(TAggregate aggregate) => _db.Set<TAggregate>().Update(aggregate);

    public void Remove(TAggregate aggregate) => _db.Set<TAggregate>().Remove(aggregate);
}
