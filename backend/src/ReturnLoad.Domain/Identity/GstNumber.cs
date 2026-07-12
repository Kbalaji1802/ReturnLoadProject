using System.Text.RegularExpressions;
using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Identity;

/// <summary>
/// An Indian GSTIN for a carrier organisation. 15 characters: 2-digit state code, 10-char
/// PAN, entity digit, a fixed <c>Z</c>, and a checksum char. Structural validation only.
/// </summary>
public sealed partial class GstNumber : ValueObject
{
    private GstNumber(string value) => Value = value;

    public string Value { get; }

    public static GstNumber Create(string? input)
    {
        string raw = Guard.AgainstNullOrWhiteSpace(input, "GST number", "gst_required").ToUpperInvariant();
        Guard.Against(!GstPattern().IsMatch(raw), "GST number format is invalid.", "gst_invalid");
        return new GstNumber(raw);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^\d{2}[A-Z]{5}\d{4}[A-Z]\d[A-Z]\d$")]
    private static partial Regex GstPattern();
}
