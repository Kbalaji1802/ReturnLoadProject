using ReturnLoad.Domain.Common;

namespace ReturnLoad.Domain.ValueObjects;

/// <summary>
/// A half-open time interval [Start, End). Models a load's pickup window and a trip's
/// availability window; <see cref="Overlaps"/> supports the pickup-time matching filter
/// (<c>MATCHING_ENGINE.md</c> §2, filter 5).
/// <para><b>Invariant:</b> End must be strictly after Start.</para>
/// </summary>
public sealed class TimeWindow : ValueObject
{
    private TimeWindow(DateTimeOffset start, DateTimeOffset end)
    {
        Start = start;
        End = end;
    }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public TimeSpan Duration => End - Start;

    public static TimeWindow Create(DateTimeOffset start, DateTimeOffset end)
    {
        Guard.Against(end <= start, "Time window end must be after its start.", "time_window_invalid");
        return new TimeWindow(start, end);
    }

    /// <summary>True when this window and <paramref name="other"/> share any instant.</summary>
    public bool Overlaps(TimeWindow other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start < other.End && other.Start < End;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}
