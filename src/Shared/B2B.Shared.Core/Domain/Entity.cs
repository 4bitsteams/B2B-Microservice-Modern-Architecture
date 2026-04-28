namespace B2B.Shared.Core.Domain;

/// <summary>
/// Base class for all domain entities.
///
/// Equality is identity-based: two entity instances are equal if and only if
/// they have the same runtime type and the same <see cref="Id"/> value.
/// This follows DDD's definition of an entity as an object whose identity
/// persists across state changes, distinguishing it from value objects whose
/// equality is component-based.
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier (e.g. <see cref="Guid"/>).</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    /// <summary>The entity's unique identifier.</summary>
    public TId Id { get; protected init; } = default!;

    /// <summary>Parameterless constructor required by EF Core and serializers.</summary>
    protected Entity() { }

    /// <summary>Creates an entity with a known identity.</summary>
    /// <param name="id">The entity's unique identifier.</param>
    protected Entity(TId id) => Id = id;

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is Entity<TId> entity && Equals(entity);

    /// <summary>
    /// Compares this entity to <paramref name="other"/> by type and <see cref="Id"/>.
    /// </summary>
    public bool Equals(Entity<TId>? other) =>
        other is not null && GetType() == other.GetType() && Id.Equals(other.Id);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    /// <summary>Returns <see langword="true"/> when both sides refer to the same entity.</summary>
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Returns <see langword="true"/> when the two sides refer to different entities.</summary>
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
}
