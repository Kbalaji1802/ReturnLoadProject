using System.Text.RegularExpressions;
using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Identity;

/// <summary>
/// An Aadhaar identity number (India KYC — Trust &amp; Safety pillar 1). This is highly
/// sensitive PII: per <c>08_TRUST_AND_SAFETY.md</c> §1 and <c>01_PROJECT_RULES.md</c> §5,6
/// we minimise and protect it — the raw value must be <b>encrypted at rest</b> and only a
/// <see cref="Masked"/> form shown in UI/logs. Modelled here so the rule travels with the
/// type; storage/encryption is enforced by the persistence layer (a later milestone).
/// Structurally validated as 12 digits not starting with 0 or 1 (UIDAI rule); the checksum
/// and authenticity are validated by the KYC source, not here.
/// </summary>
public sealed partial class AadhaarNumber : ValueObject
{
    private AadhaarNumber(string value) => Value = value;

    /// <summary>The 12-digit value. Sensitive — never log; encrypt at rest.</summary>
    public string Value { get; }

    /// <summary>Safe-to-display form, revealing only the last four digits.</summary>
    public string Masked => $"XXXX XXXX {Value[8..]}";

    public static AadhaarNumber Create(string? input)
    {
        string raw = Guard.AgainstNullOrWhiteSpace(input, "Aadhaar number", "aadhaar_required");
        string digits = Separators().Replace(raw, string.Empty);
        Guard.Against(!AadhaarPattern().IsMatch(digits), "Aadhaar number must be 12 digits.", "aadhaar_invalid");
        return new AadhaarNumber(digits);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>Renders the masked form — intentionally never the raw value.</summary>
    public override string ToString() => Masked;

    [GeneratedRegex(@"[\s-]")]
    private static partial Regex Separators();

    [GeneratedRegex(@"^[2-9]\d{11}$")]
    private static partial Regex AadhaarPattern();
}
