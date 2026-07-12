using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ReturnLoad.Infrastructure.Security;

namespace ReturnLoad.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> to build the context for migrations
/// without booting the app or needing a live database. The connection string here is a
/// placeholder for scaffolding only — it is never used at runtime.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=returnload_design;Username=design;Password=design")
                .Options;

        return new ApplicationDbContext(options, new NoOpFieldEncryptor());
    }
}
