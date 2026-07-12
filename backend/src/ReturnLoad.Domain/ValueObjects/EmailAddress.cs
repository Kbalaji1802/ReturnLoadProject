using System.Text.RegularExpressions;
using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.ValueObjects;

/// <summary>
/// An email address, normalised to lowercase. Format-validated only — deliverability and
/// ownership are confirmed elsewhere (verification is a later milestone).
/// </summary>
public sealed partial class EmailAddress : ValueObject
{
    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    public static EmailAddress Create(string? input)
    {
        string raw = Guard.AgainstNullOrWhiteSpace(input, "Email", "email_required").ToLowerInvariant();
        Guard.Against(!EmailPattern().IsMatch(raw), "Email format is invalid.", "email_invalid");
        return new EmailAddress(raw);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
