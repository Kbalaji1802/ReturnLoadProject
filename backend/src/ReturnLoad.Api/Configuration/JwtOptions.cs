namespace ReturnLoad.Api.Configuration;

/// <summary>
/// Strongly-typed JWT bearer settings, bound from the "Jwt" configuration section.
/// <para>
/// This wires the authentication <b>framework</b> only. There is deliberately no
/// token issuance, user store, or login endpoint in the foundation — Identity &amp;
/// Access is task T-013 (05_NEXT_TASKS.md). Real signing keys are supplied via
/// environment variables, never committed (01_PROJECT_RULES.md §1).
/// </para>
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;
}
