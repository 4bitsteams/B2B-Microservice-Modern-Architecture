using MediatR;

namespace B2B.Shared.Core.Domain;

/// <summary>
/// Marker interface for domain events — facts that describe something that
/// happened within a single aggregate's consistency boundary.
///
/// Domain events are internal to a service. They are raised by aggregate
/// mutation methods (e.g. <c>order.Confirm()</c>) and published by
/// <c>DomainEventBehavior</c> after <c>SaveChangesAsync</c> completes.
/// For cross-service communication use integration events instead.
///
/// Implementing <see cref="INotification"/> allows MediatR to fan out the
/// event to multiple handlers registered in the same process.
/// </summary>
public interface IDomainEvent : INotification
{
    /// <summary>Unique identifier for this event instance, generated at raise time.</summary>
    Guid EventId { get; }

    /// <summary>UTC timestamp of when the event was raised inside the aggregate.</summary>
    DateTime OccurredOn { get; }
}

/// <summary>
/// Immutable base record for concrete domain events.
/// Derive from this record to define events, then raise them via
/// <c>AggregateRoot.RaiseDomainEvent</c>:
/// <code>
/// public record OrderConfirmedEvent(Guid OrderId, string OrderNumber) : DomainEvent;
/// </code>
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; } = Guid.NewGuid();

    /// <inheritdoc/>
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
