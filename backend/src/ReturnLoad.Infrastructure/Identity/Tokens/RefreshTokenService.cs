using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReturnLoad.Infrastructure.Persistence;
using ReturnLoad.Shared.Configuration;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Infrastructure.Identity.Tokens;

/// <summary>
/// Refresh tokens backed by the <see cref="RefreshToken"/> table. Raw tokens are 256-bit
/// random values; only their SHA-256 hash is stored. Rotation is one-time-use with
/// family-wide revocation on reuse (ADR-0013).
/// </summary>
internal sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly ApplicationDbContext _db;
    private readonly JwtOptions _options;

    public RefreshTokenService(ApplicationDbContext db, IOptions<JwtOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<IssuedRefreshToken> IssueAsync(Guid userId, string? deviceId, CancellationToken cancellationToken = default)
    {
        (string raw, RefreshToken entity) = CreateToken(userId, Guid.NewGuid(), deviceId);
        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return new IssuedRefreshToken(raw, entity.ExpiresAtUtc);
    }

    public async Task<Result<RefreshRotation>> RotateAsync(string rawToken, string? deviceId, CancellationToken cancellationToken = default)
    {
        string hash = Hash(rawToken);
        RefreshToken? current = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (current is null)
        {
            return Error.Unauthorized("Invalid refresh token.");
        }

        // Reuse of an already-revoked token → the family may be compromised: revoke it all.
        if (current.RevokedAtUtc is not null)
        {
            await RevokeFamilyAsync(current.UserId, current.FamilyId, cancellationToken);
            return Error.Unauthorized("Refresh token reuse detected; session revoked.");
        }

        if (DateTimeOffset.UtcNow >= current.ExpiresAtUtc)
        {
            return Error.Unauthorized("Refresh token has expired.");
        }

        // Rotate: issue a replacement in the same family, revoke the current one.
        (string raw, RefreshToken replacement) = CreateToken(current.UserId, current.FamilyId, deviceId ?? current.DeviceId);
        current.RevokedAtUtc = DateTimeOffset.UtcNow;
        current.ReplacedByTokenHash = replacement.TokenHash;
        _db.RefreshTokens.Add(replacement);
        await _db.SaveChangesAsync(cancellationToken);

        return new RefreshRotation(current.UserId, new IssuedRefreshToken(raw, replacement.ExpiresAtUtc));
    }

    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        string hash = Hash(rawToken);
        RefreshToken? token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is { RevokedAtUtc: null })
        {
            token.RevokedAtUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        RevokeWhereAsync(t => t.UserId == userId && t.RevokedAtUtc == null, cancellationToken);

    private Task RevokeFamilyAsync(Guid userId, Guid familyId, CancellationToken cancellationToken) =>
        RevokeWhereAsync(t => t.UserId == userId && t.FamilyId == familyId && t.RevokedAtUtc == null, cancellationToken);

    // Load-and-save (rather than ExecuteUpdate) so the same code runs on every EF provider,
    // including the in-memory store used by tests. The affected set per user is small.
    private async Task RevokeWhereAsync(
        System.Linq.Expressions.Expression<Func<RefreshToken, bool>> predicate,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<RefreshToken> tokens = await _db.RefreshTokens.Where(predicate).ToListAsync(cancellationToken);
        foreach (RefreshToken token in tokens)
        {
            token.RevokedAtUtc = now;
        }

        if (tokens.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private (string Raw, RefreshToken Entity) CreateToken(Guid userId, Guid familyId, string? deviceId)
    {
        string raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        RefreshToken entity = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = Hash(raw),
            DeviceId = deviceId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_options.RefreshTokenDays),
        };
        return (raw, entity);
    }

    private static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
}
