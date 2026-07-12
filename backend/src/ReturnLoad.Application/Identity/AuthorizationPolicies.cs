namespace ReturnLoad.Application.Identity;

/// <summary>
/// Named authorization policies and their role mapping (design review §3, ADR-0013).
/// Endpoints authorize against a <b>policy name</b>, never a hard-coded role list, so the
/// role→capability mapping lives here in one place. This is also the seam for future
/// fine-grained permissions: today a policy is satisfied by role membership; later it can
/// be satisfied by permission claims without changing any call site.
/// </summary>
public static class AuthorizationPolicies
{
    public const string InternalStaff = "InternalStaff";
    public const string CanManageRoles = "CanManageRoles";
    public const string CanVerifyDocuments = "CanVerifyDocuments";
    public const string CanManageDisputes = "CanManageDisputes";
    public const string CanBlacklist = "CanBlacklist";
    public const string CanManageCarrierFleet = "CanManageCarrierFleet";
    public const string CanPostLoads = "CanPostLoads";

    /// <summary>Policy → the roles that satisfy it (the initial, role-based mapping).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> RoleMap = new Dictionary<string, string[]>
    {
        [InternalStaff] = [Roles.PlatformAdmin, Roles.Operations, Roles.Finance, Roles.Support],
        [CanManageRoles] = [Roles.PlatformAdmin],
        [CanVerifyDocuments] = [Roles.Operations],
        [CanManageDisputes] = [Roles.Operations, Roles.Support],
        [CanBlacklist] = [Roles.PlatformAdmin, Roles.Operations],
        [CanManageCarrierFleet] = [Roles.CarrierOwner, Roles.Dispatcher],
        [CanPostLoads] = [Roles.Shipper],
    };
}
