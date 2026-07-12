namespace ReturnLoad.Application.Identity;

/// <summary>
/// The platform's nine authorization roles (`03_TECHNICAL_BIBLE.md` §13, ADR-0013).
/// Role names are stable identifiers carried as JWT claims; treat changes as breaking.
/// Endpoints authorize via <b>policies</b> (see <c>AuthorizationPolicies</c>), not by
/// hard-coding these names, so the role→capability mapping lives in one place.
/// </summary>
public static class Roles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string Operations = "Operations";
    public const string Finance = "Finance";
    public const string Support = "Support";
    public const string CarrierOwner = "CarrierOwner";
    public const string Dispatcher = "Dispatcher";
    public const string Driver = "Driver";
    public const string Shipper = "Shipper";
    public const string AssociationManager = "AssociationManager";

    /// <summary>All roles — used to seed the role store at startup.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        PlatformAdmin, Operations, Finance, Support,
        CarrierOwner, Dispatcher, Driver, Shipper, AssociationManager,
    ];

    /// <summary>Internal (staff) roles.</summary>
    public static readonly IReadOnlyList<string> Internal =
        [PlatformAdmin, Operations, Finance, Support];
}
