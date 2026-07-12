namespace ReturnLoad.Api.Configuration;

/// <summary>
/// Transport-level request limits, bound from the "RequestLimits" section. Capping the
/// request body size is a cheap, broad defence against memory-exhaustion abuse
/// (03_TECHNICAL_BIBLE.md §7); endpoints that legitimately need more (file uploads,
/// M6) raise it locally.
/// </summary>
public sealed class RequestLimitOptions
{
    public const string SectionName = "RequestLimits";

    /// <summary>Maximum request body size in bytes. Default 10 MB.</summary>
    public long MaxRequestBodyBytes { get; init; } = 10 * 1024 * 1024;
}
