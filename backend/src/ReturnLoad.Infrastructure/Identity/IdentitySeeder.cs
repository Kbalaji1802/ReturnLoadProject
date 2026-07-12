using Microsoft.AspNetCore.Identity;
using ReturnLoad.Application.Identity;

namespace ReturnLoad.Infrastructure.Identity;

/// <summary>
/// Ensures the nine platform roles (`03_TECHNICAL_BIBLE.md` §13) exist in the role store.
/// Invoked as a deploy/startup step once the schema is present (migrations applied); the
/// auth core (register/login/refresh) does not depend on roles being pre-seeded.
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager, CancellationToken cancellationToken = default)
    {
        foreach (string role in Roles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole(role));
            }
        }
    }
}
