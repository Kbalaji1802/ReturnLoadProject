namespace ReturnLoad.Shared.Configuration;

/// <summary>
/// Strongly-typed JWT settings, bound from the "Jwt" configuration section. Lives in
/// Shared so the token issuer (Infrastructure) and the bearer-validation setup (API) read
/// one source of truth (ADR-0013). Real signing keys come from the environment / a secret
/// store, never committed (01_PROJECT_RULES.md §1); startup fails fast outside Development
/// if they are missing (JwtOptionsValidator).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Access-token lifetime in minutes (ADR-0013: 15).</summary>
    public int AccessTokenMinutes { get; init; } = 15;

    /// <summary>Refresh-token lifetime in days (ADR-0013: 7).</summary>
    public int RefreshTokenDays { get; init; } = 7;
}
