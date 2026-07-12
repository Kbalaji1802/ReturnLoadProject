namespace ReturnLoad.Domain.Common;

/// <summary>
/// Guard clauses for enforcing domain invariants. Each failure throws a
/// <see cref="DomainException"/> with a stable code, keeping entity and value-object
/// constructors terse while making the rule they enforce explicit and testable.
/// </summary>
public static class Guard
{
    /// <summary>Throws when <paramref name="condition"/> is true.</summary>
    public static void Against(bool condition, string message, string code)
    {
        if (condition)
        {
            throw new DomainException(message, code);
        }
    }

    public static string AgainstNullOrWhiteSpace(string? value, string field, string code)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{field} is required.", code);
        }

        return value.Trim();
    }

    public static decimal AgainstNegativeOrZero(decimal value, string field, string code)
    {
        if (value <= 0)
        {
            throw new DomainException($"{field} must be greater than zero.", code);
        }

        return value;
    }

    public static decimal AgainstNegative(decimal value, string field, string code)
    {
        if (value < 0)
        {
            throw new DomainException($"{field} cannot be negative.", code);
        }

        return value;
    }

    public static void AgainstDefault<T>(T value, string field, string code)
        where T : struct
    {
        if (value.Equals(default(T)))
        {
            throw new DomainException($"{field} is required.", code);
        }
    }
}
