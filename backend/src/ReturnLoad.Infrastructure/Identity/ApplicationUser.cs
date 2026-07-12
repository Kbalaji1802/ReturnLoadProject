using Microsoft.AspNetCore.Identity;
using ReturnLoad.Application.Identity;

namespace ReturnLoad.Infrastructure.Identity;

/// <summary>
/// The authentication anchor (ADR-0013). Extends ASP.NET Core Identity's user with only
/// what authentication needs; profile/domain data (name, Driver/Shipper, KYC) lives in
/// separate entities keyed by <c>Id</c>. Keeping this lean keeps the Identity table
/// focused and avoids coupling auth to business concerns.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Authentication lifecycle state (distinct from trust verification, M6).</summary>
    public AccountStatus AccountStatus { get; set; } = AccountStatus.PendingVerification;

    /// <summary>
    /// Bumped when roles/permissions change; issued as the <c>permissionsVersion</c> claim so
    /// outstanding access tokens can be invalidated by version comparison (ADR-0013).
    /// </summary>
    public int PermissionsVersion { get; set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
