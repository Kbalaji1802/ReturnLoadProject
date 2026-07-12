namespace ReturnLoad.Infrastructure.Identity;

/// <summary>
/// A server-side refresh-token record (ADR-0013). The raw token is returned to the client
/// once and never stored — only its SHA-256 hash lives here, so a DB leak yields no usable
/// tokens. Tokens are single-use: each refresh revokes the current row and issues a new one
/// in the same <see cref="FamilyId"/> (session), enabling reuse detection.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>SHA-256 (hex) of the raw token. Unique.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>The session lineage — all rotations of one login share a family.</summary>
    public Guid FamilyId { get; set; }

    /// <summary>Optional device/session identifier supplied by the client.</summary>
    public string? DeviceId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    /// <summary>Hash of the token that replaced this one on rotation (audit trail).</summary>
    public string? ReplacedByTokenHash { get; set; }
}
