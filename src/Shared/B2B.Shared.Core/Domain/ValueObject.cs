namespace B2B.Shared.Core.Domain;

/// <summary>
/// Base class for DDD Value Objects.
///
/// Value objects are immutable types whose identity is defined entirely by
/// their component values — two instances with the same components are equal,
/// regardless of reference. This contrasts with entities, whose identity is
/// defined by a unique ID.
///
/// Derive from this class and override <see cref="GetEqualityComponents"/> to
/// list the properties that participate in equality:
/// <code>
/// public sealed class Money : ValueObject
/// {
///     public decimal Amount { get; }
///     public string Currency { get; }
///
///     protected override IEnumerable&lt;object?&gt; GetEqualityComponents()
///     {
///         yield return Amount;
///         yield return Currency;
///     }
/// }
/// </code>
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>
    /// Returns the ordered sequence of components that define this value object's
    /// equality. All non-identity fields that distinguish one instance from another
    /// should be yielded here.
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is ValueObject other && Equals(other);

    /// <summary>
    /// Compares this value object to <paramref name="other"/> by type and components.
    /// </summary>
    public bool Equals(ValueObject? other) =>
        other is not null && GetType() == other.GetType() &&
        GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    /// <inheritdoc/>
    public override int GetHashCode() =>
        GetEqualityComponents()
            .Aggregate(0, (hash, obj) => HashCode.Combine(hash, obj?.GetHashCode() ?? 0));

    /// <summary>Returns <see langword="true"/> when both value objects are structurally equal.</summary>
    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Returns <see langword="true"/> when the two value objects are not structurally equal.</summary>
    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
