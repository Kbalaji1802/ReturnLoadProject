using System.Text.RegularExpressions;
using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Identity;

/// <summary>
/// An Indian driving licence number (verified against SARATHI/Parivahan later — see
/// <c>08_TRUST_AND_SAFETY.md</c>). Normalised to uppercase without spaces/hyphens. Format:
/// two-letter state code, then digits — commonly 13–16 characters overall
/// (e.g. <c>TN0120200001234</c>). Validation is structural only; authenticity is a
/// verification concern.
/// </summary>
public sealed partial class DrivingLicenceNumber : ValueObject
{
    private DrivingLicenceNumber(string value) => Value = value;

    public string Value { get; }

    public static DrivingLicenceNumber Create(string? input)
    {
        string raw = Guard.AgainstNullOrWhiteSpace(input, "Driving licence number", "dl_required");
        string normalised = Separators().Replace(raw, string.Empty).ToUpperInvariant();
        Guard.Against(
            !LicencePattern().IsMatch(normalised),
            "Driving licence number format is invalid.",
            "dl_invalid");
        return new DrivingLicenceNumber(normalised);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"[\s-]")]
    private static partial Regex Separators();

    // State code (2 letters) + 11–14 digits/alphanumerics.
    [GeneratedRegex(@"^[A-Z]{2}[A-Z0-9]{11,14}$")]
    private static partial Regex LicencePattern();
}
