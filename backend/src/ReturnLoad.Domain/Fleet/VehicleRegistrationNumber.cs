using System.Text.RegularExpressions;
using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Fleet;

/// <summary>
/// An Indian vehicle registration (number plate), e.g. <c>TN01AB1234</c>. Normalised to
/// uppercase without spaces/hyphens. Format: state code (2 letters), RTO code (1–2 digits),
/// series (1–3 letters), and a 1–4 digit number. Uniqueness across the platform is a
/// persistence concern (unique index) enforced when EF configurations land.
/// </summary>
public sealed partial class VehicleRegistrationNumber : ValueObject
{
    private VehicleRegistrationNumber(string value) => Value = value;

    public string Value { get; }

    public static VehicleRegistrationNumber Create(string? input)
    {
        string raw = Guard.AgainstNullOrWhiteSpace(input, "Registration number", "registration_required");
        string normalised = Separators().Replace(raw, string.Empty).ToUpperInvariant();
        Guard.Against(
            !PlatePattern().IsMatch(normalised),
            "Vehicle registration number format is invalid.",
            "registration_invalid");
        return new VehicleRegistrationNumber(normalised);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"[\s-]")]
    private static partial Regex Separators();

    [GeneratedRegex(@"^[A-Z]{2}\d{1,2}[A-Z]{1,3}\d{1,4}$")]
    private static partial Regex PlatePattern();
}
