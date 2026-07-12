namespace ReturnLoad.Api.Configuration;

/// <summary>
/// Toggles for the security response headers and HTTPS policy, bound from the
/// "Security" section (03_TECHNICAL_BIBLE.md §7). Defaults are hardened; individual
/// switches exist so environments (notably in-memory tests, which run over plain HTTP)
/// can relax a specific control without disabling the rest.
/// </summary>
public sealed class SecurityHeaderOptions
{
    public const string SectionName = "Security";

    /// <summary>Redirect HTTP → HTTPS. Off in Development and tests; on elsewhere.</summary>
    public bool HttpsRedirection { get; init; } = true;

    /// <summary>Emit HSTS (only meaningful over HTTPS; paired with <see cref="HttpsRedirection"/>).</summary>
    public bool Hsts { get; init; } = true;

    /// <summary>Emit the static security headers (nosniff, frame-options, referrer, CSP).</summary>
    public bool SecurityHeaders { get; init; } = true;

    /// <summary>The Content-Security-Policy for API responses. Restrictive by default.</summary>
    public string ContentSecurityPolicy { get; init; } = "default-src 'none'; frame-ancestors 'none'";
}
