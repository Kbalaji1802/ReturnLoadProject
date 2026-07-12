using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.Reviews;

/// <summary>
/// A star rating from 1 to 5 — the post-trip trust signal each party leaves for the other
/// (Business Bible §8; also a matching tie-breaker, <c>MATCHING_ENGINE.md</c> §3).
/// <para><b>Invariant:</b> value must be between 1 and 5 inclusive.</para>
/// </summary>
public sealed class Rating : ValueObject
{
    public const int Minimum = 1;
    public const int Maximum = 5;

    private Rating(int stars) => Stars = stars;

    public int Stars { get; }

    public static Rating Of(int stars)
    {
        Guard.Against(stars is < Minimum or > Maximum, $"Rating must be between {Minimum} and {Maximum}.", "rating_out_of_range");
        return new Rating(stars);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Stars;
    }

    public override string ToString() => $"{Stars}/{Maximum}";
}
