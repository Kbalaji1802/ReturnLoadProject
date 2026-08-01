namespace ReturnLoad.Application.UseCases.Trips;

/// <summary>
/// Tunable trip-lifecycle parameters (Part 5). Config-driven, not hard-coded (01_PROJECT_RULES.md).
/// </summary>
public sealed class TripOptions
{
    public const string SectionName = "Trips";

    /// <summary>
    /// How long the driver must wait for the load owner to confirm a pickup/delivery gate before the
    /// driver may self-advance it (recorded as auto-confirmed) — so a truck is never stranded on an
    /// absent owner. Blocking with a timeout fallback (correction-sprint decision).
    /// </summary>
    public int OwnerConfirmWindowMinutes { get; set; } = 15;

    public TimeSpan OwnerConfirmWindow => TimeSpan.FromMinutes(OwnerConfirmWindowMinutes);
}
