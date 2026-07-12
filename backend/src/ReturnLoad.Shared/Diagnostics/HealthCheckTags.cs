namespace ReturnLoad.Shared.Diagnostics;

/// <summary>
/// Well-known health-check tags shared between the layer that registers checks
/// (Infrastructure) and the layer that maps probe endpoints (Api), so the two
/// never drift apart on a magic string.
/// </summary>
public static class HealthCheckTags
{
    /// <summary>Checks that must pass for the service to accept traffic (e.g. the database).</summary>
    public const string Ready = "ready";
}
