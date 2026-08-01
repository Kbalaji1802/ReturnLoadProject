using ReturnLoad.Domain.Loads;

namespace ReturnLoad.Application.UseCases.Matching;

/// <summary>
/// Tunable ranking parameters for the matching engine (M5). Config-driven, not hard-coded
/// constants (MATCHING_ENGINE.md §2 "tunable parameters", 01_PROJECT_RULES.md) — adjustable
/// per-lane later without a code change. Weights are relative and normalised to a 0–100 score.
/// </summary>
public sealed class MatchingOptions
{
    public const string SectionName = "Matching";

    /// <summary>Beyond this pickup distance a load scores ~0 on proximity (the dominant signal).</summary>
    public double MaxPickupRadiusKm { get; set; } = 300;

    /// <summary>Pickup radius (km) for an <see cref="AreaType.Urban"/> pickup — the hard filter (Part 2).</summary>
    public double UrbanRadiusKm { get; set; } = 5;

    /// <summary>Pickup radius (km) for a <see cref="AreaType.Suburban"/> pickup — the hard filter (Part 2).</summary>
    public double SuburbanRadiusKm { get; set; } = 10;

    /// <summary>Pickup radius (km) for a <see cref="AreaType.Highway"/> pickup — the hard filter (Part 2).</summary>
    public double HighwayRadiusKm { get; set; } = 25;

    /// <summary>
    /// Resolves the configured pickup radius for a load's pickup area type (Part 2 hard filter).
    /// A driver farther than this from the pickup is excluded from the load, not merely down-ranked.
    /// </summary>
    public double ResolvePickupRadiusKm(AreaType areaType) => areaType switch
    {
        AreaType.Urban => UrbanRadiusKm,
        AreaType.Highway => HighwayRadiusKm,
        _ => SuburbanRadiusKm,
    };

    /// <summary>Haul length that earns the full haul-length score (longer = more earning).</summary>
    public double MaxHaulKm { get; set; } = 600;

    /// <summary>Weight of pickup proximity to the driver (the biggest factor in the example).</summary>
    public int ProximityWeight { get; set; } = 60;

    /// <summary>Weight of how fully the load uses the truck's payload capacity.</summary>
    public int UtilizationWeight { get; set; } = 25;

    /// <summary>Weight of haul length (earning potential).</summary>
    public int HaulWeight { get; set; } = 15;

    /// <summary>Bonus weight for a well-reviewed load owner (partner reputation, M8). Additive;
    /// the final score is clamped to 100.</summary>
    public int RatingWeight { get; set; } = 10;
}
