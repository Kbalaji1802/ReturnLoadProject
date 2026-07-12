namespace ReturnLoad.Application.Abstractions.Persistence;

/// <summary>
/// Commits a set of changes made through repositories as one atomic unit. Implemented by the
/// EF Core DbContext. Keeping this behind an interface lets application code save work
/// without depending on EF (Clean Architecture, ADR-0006).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
