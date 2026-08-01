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
        // Design-time only. Prefer an explicit connection from the environment so `dotnet ef`
        // (e.g. `database update`) can target a real database such as Neon without hard-coding
        // any secret; fall back to a local placeholder for offline scaffolding (`migrations add`).
        string connection =
            Environment.GetEnvironmentVariable("ConnectionStrings__ReturnLoadDatabase")
            ?? "Host=localhost;Port=5432;Database=returnload_design;Username=design;Password=design";

        DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connection)
                .Options;

        return new ApplicationDbContext(options, new NoOpFieldEncryptor());
    }
}
