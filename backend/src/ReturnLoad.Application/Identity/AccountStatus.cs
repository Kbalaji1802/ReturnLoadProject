namespace ReturnLoad.Application.Identity;

/// <summary>
/// The authentication lifecycle state of a <c>User</c> (design review §4, ADR-0013).
/// This governs whether the account can authenticate — it is deliberately separate from
/// <b>trust verification / transactability</b>, which the Trust &amp; Safety context (M6)
/// owns (design review §11). An account can be <see cref="Active"/> yet not allowed to
/// transact until its documents are verified.
/// </summary>
public enum AccountStatus
{
    /// <summary>Registered but email/phone not yet confirmed (confirmation ships later).</summary>
    PendingVerification = 0,

    /// <summary>Normal operating account — may authenticate.</summary>
    Active = 1,

    /// <summary>Deliberate, reversible admin action (investigation/policy) — cannot authenticate.</summary>
    Suspended = 2,

    /// <summary>Automatic, temporary lock from too many failed logins — cannot authenticate until it expires.</summary>
    Locked = 3,

    /// <summary>Deactivated (user- or admin-initiated) — cannot authenticate.</summary>
    Disabled = 4,
}
