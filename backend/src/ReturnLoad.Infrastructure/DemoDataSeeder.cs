using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReturnLoad.Application.Identity;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.ValueObjects;
using ReturnLoad.Infrastructure.Identity;
using ReturnLoad.Infrastructure.Persistence;

namespace ReturnLoad.Infrastructure;

/// <summary>
/// Seeds demo users, roles, a shipper profile, and a demo carrier for local development so
/// the full flow can be exercised end-to-end. Idempotent; invoked only in Development.
/// </summary>
public static class DemoDataSeeder
{
    /// <summary>Deterministic id for the demo carrier so onboarding instructions can reference it.</summary>
    public static readonly Guid DemoCarrierId = new("11111111-1111-1111-1111-111111111111");

    public const string Password = "Passw0rd!Sprint";

    private static readonly (string Email, string[] Roles)[] Users =
    [
        ("admin@returnload.test", [Roles.PlatformAdmin, Roles.Operations]),
        ("carrier@returnload.test", [Roles.CarrierOwner, Roles.Dispatcher]),
        ("driver@returnload.test", [Roles.Driver]),
        ("shipper@returnload.test", [Roles.Shipper]),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        RoleManager<ApplicationRole> roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        UserManager<ApplicationUser> userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationDbContext db = services.GetRequiredService<ApplicationDbContext>();

        await IdentitySeeder.SeedRolesAsync(roleManager, cancellationToken);

        foreach ((string email, string[] roles) in Users)
        {
            if (await userManager.FindByEmailAsync(email) is not null)
            {
                continue;
            }

            ApplicationUser user = new()
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                AccountStatus = AccountStatus.Active,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            };

            IdentityResult created = await userManager.CreateAsync(user, Password);
            if (created.Succeeded)
            {
                await userManager.AddToRolesAsync(user, roles);

                // The shipper needs a UserProfile so it can post loads.
                if (email == "shipper@returnload.test")
                {
                    db.UserProfiles.Add(UserProfile.Create(
                        user.Id, "Demo Shipper", MobileNumber.Create("9800000001"), EmailAddress.Create(email)));
                }
            }
        }

        // A demo carrier so vehicles/trips can be registered against a known id.
        if (!await db.Carriers.AnyAsync(c => c.Id == DemoCarrierId, cancellationToken))
        {
            Carrier carrier = Carrier.Register("Demo Logistics (TN)", MobileNumber.Create("9800000000"));

            // Seed-only: pin the demo carrier to a known id via its protected setter so the
            // onboarding instructions can reference it. Never done in business code.
            typeof(Carrier).GetProperty(nameof(Carrier.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(carrier, [DemoCarrierId]);

            carrier.Activate();
            db.Carriers.Add(carrier);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
