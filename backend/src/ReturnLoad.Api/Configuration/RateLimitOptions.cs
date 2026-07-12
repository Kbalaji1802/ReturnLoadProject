namespace ReturnLoad.Api.Configuration;

/// <summary>
/// Rate-limiting policy, bound from the "RateLimiting" section — the first line of
/// API-abuse protection (03_TECHNICAL_BIBLE.md §7). A permissive global limit protects
/// the platform without hindering normal use; a stricter <see cref="Sensitive"/> limit
/// is reserved for high-risk endpoints (login, OTP) once M2 adds them.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>The default per-client limit applied globally.</summary>
    public RateLimitWindow Global { get; init; } = new() { PermitLimit = 100, WindowSeconds = 60 };

    /// <summary>A tighter limit for sensitive endpoints (applied per-endpoint in M2).</summary>
    public RateLimitWindow Sensitive { get; init; } = new() { PermitLimit = 10, WindowSeconds = 60 };
}

/// <summary>A fixed-window limit: at most <see cref="PermitLimit"/> requests per window.</summary>
public sealed class RateLimitWindow
{
    public int PermitLimit { get; init; } = 100;

    public int WindowSeconds { get; init; } = 60;

    /// <summary>How many requests may queue rather than being rejected immediately.</summary>
    public int QueueLimit { get; init; }
}
