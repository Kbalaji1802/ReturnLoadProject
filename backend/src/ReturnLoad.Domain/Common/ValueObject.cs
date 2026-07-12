namespace ReturnLoad.Domain.Common;

/// <summary>
/// Base class for value objects — immutable types with no identity, compared by the value
/// of their components rather than by reference (e.g. two <c>Money</c> of ₹100 are equal).
/// Subclasses yield their components via <see cref="GetEqualityComponents"/>; equality and
/// hashing derive from that sequence.
/// </summary>
public abstract class ValueObject
{
    /// <summary>The components whose values define equality, in a stable order.</summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        ValueObject other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        HashCode hash = default;
        foreach (object? component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
