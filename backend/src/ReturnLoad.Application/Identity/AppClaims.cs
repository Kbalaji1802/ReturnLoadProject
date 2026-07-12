namespace ReturnLoad.Application.Identity;

/// <summary>
/// Custom JWT claim types issued alongside the standard ones (`sub`, `jti`, `iat`,
/// `exp`, role) — see design review §"token claims" / ADR-0013.
/// </summary>
public static class AppClaims
{
    /// <summary>The user's stable id (mirrors <c>sub</c> for convenience).</summary>
    public const string UserId = "userId";

    /// <summary>
    /// Bumped whenever a user's roles/permissions change. Comparing the token's value to
    /// the current value lets us invalidate outstanding access tokens after a permission
    /// change without redesigning the token format.
    /// </summary>
    public const string PermissionsVersion = "permissionsVersion";

    /// <summary>Reserved for future multi-tenant scoping (Carrier isolation).</summary>
    public const string TenantId = "tenantId";

    /// <summary>Optional device/session identifier this token was issued to.</summary>
    public const string DeviceId = "deviceId";
}
