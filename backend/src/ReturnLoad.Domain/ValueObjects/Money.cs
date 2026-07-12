using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.ValueObjects;

/// <summary>
/// A monetary amount in a given currency. Defaults to INR (₹), the market currency
/// (ADR-0004). Amounts are non-negative; arithmetic is only allowed within the same
/// currency (mixing currencies is a domain error, not a silent conversion).
/// </summary>
public sealed class Money : ValueObject
{
    public const string DefaultCurrency = "INR";

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Zero { get; } = new(0m, DefaultCurrency);

    public static Money Of(decimal amount, string currency = DefaultCurrency)
    {
        Guard.AgainstNegative(amount, "Amount", "money_negative");
        string normalised = Guard.AgainstNullOrWhiteSpace(currency, "Currency", "currency_required").ToUpperInvariant();
        Guard.Against(normalised.Length != 3, "Currency must be a 3-letter ISO code.", "currency_invalid");
        return new Money(amount, normalised);
    }

    public Money Add(Money other)
    {
        Guard.Against(other.Currency != Currency, "Cannot add money of different currencies.", "currency_mismatch");
        return new Money(Amount + other.Amount, Currency);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Currency} {Amount:0.00}";
}
