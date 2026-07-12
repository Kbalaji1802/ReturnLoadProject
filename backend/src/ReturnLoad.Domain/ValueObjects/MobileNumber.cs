using System.Text.RegularExpressions;
using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.ValueObjects;

/// <summary>
/// An Indian mobile number (market scope: India / Tamil Nadu, ADR-0004). Normalised to
/// <c>+91XXXXXXXXXX</c>. Indian mobiles are 10 digits beginning 6–9; a leading <c>0</c> or
/// <c>+91</c>/<c>91</c> country prefix is accepted and stripped.
/// </summary>
public sealed partial class MobileNumber : ValueObject
{
    private MobileNumber(string value) => Value = value;

    /// <summary>The normalised number, e.g. <c>+919876543210</c>.</summary>
    public string Value { get; }

    /// <summary>The 10-digit national number without the country code.</summary>
    public string NationalNumber => Value[3..];

    public static MobileNumber Create(string? input)
    {
        string raw = Guard.AgainstNullOrWhiteSpace(input, "Mobile number", "mobile_required");
        string digits = NonDigits().Replace(raw, string.Empty);

        // Strip country code / trunk prefix down to the 10-digit national number.
        if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (digits.Length == 11 && digits.StartsWith('0'))
        {
            digits = digits[1..];
        }

        Guard.Against(
            !TenDigitIndianMobile().IsMatch(digits),
            "Mobile number must be a valid 10-digit Indian mobile number.",
            "mobile_invalid");

        return new MobileNumber($"+91{digits}");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();

    [GeneratedRegex(@"^[6-9]\d{9}$")]
    private static partial Regex TenDigitIndianMobile();
}
