using ReturnLoad.Shared.Results;

namespace ReturnLoad.Infrastructure.Identity.Tokens;

/// <summary>
/// Issues, rotates, and revokes refresh tokens (ADR-0013). Tokens are single-use and
/// rotated on every refresh; presenting an already-used token triggers family-wide
/// revocation (reuse detection).
/// </summary>
internal interface IRefreshTokenService
{
    /// <summary>Issues a new refresh token, starting a session family.</summary>
    Task<IssuedRefreshToken> IssueAsync(Guid userId, string? deviceId, CancellationToken cancellationToken = default);

    /// <summary>Validates and rotates a refresh token; detects reuse.</summary>
    Task<Result<RefreshRotation>> RotateAsync(string rawToken, string? deviceId, CancellationToken cancellationToken = default);

    /// <summary>Revokes a single refresh token (logout this device).</summary>
    Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes every active refresh token for a user (logout all devices).</summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>A freshly issued refresh token: the raw value (returned once) and its expiry.</summary>
public sealed record IssuedRefreshToken(string RawToken, DateTimeOffset ExpiresAt);

/// <summary>The result of a successful rotation: whose token it was, and the replacement.</summary>
public sealed record RefreshRotation(Guid UserId, IssuedRefreshToken NewToken);
