namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Mixin interface applied to entities whose creation and last-update timestamps
/// are automatically tracked by the EF Core <c>SaveChanges</c> override in
/// <c>BaseDbContext</c>.
///
/// Implement on any entity that needs an audit trail. The infrastructure layer
/// sets <see cref="CreatedAt"/> on insert and <see cref="UpdatedAt"/> on every
/// subsequent save — application code never sets these fields directly.
/// </summary>
public interface IAuditableEntity
{
    /// <summary>UTC timestamp when the entity was first persisted.</summary>
    DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update, or <see langword="null"/> if never updated.</summary>
    DateTime? UpdatedAt { get; set; }
}
