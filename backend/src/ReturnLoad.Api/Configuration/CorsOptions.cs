namespace ReturnLoad.Api.Configuration;

/// <summary>
/// Cross-origin policy, bound from the "Cors" section. The browser clients (admin,
/// mobile web) live on other origins, so CORS must be explicit — never a wildcard in
/// production (03_TECHNICAL_BIBLE.md §7). An empty allowlist means no cross-origin
/// browser access, which is the safe default.
/// </summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>The exact origins permitted (scheme + host + port). Empty = none allowed.</summary>
    public string[] AllowedOrigins { get; init; } = [];

    /// <summary>
    /// Whether to allow credentials (cookies / Authorization) on cross-origin requests.
    /// Off by default; enabled deliberately if refresh tokens use cookies (decided in M2).
    /// </summary>
    public bool AllowCredentials { get; init; }
}
