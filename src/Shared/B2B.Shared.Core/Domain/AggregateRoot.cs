namespace B2B.Shared.Core.Domain;

/// <summary>
/// Base class for DDD Aggregate Roots.
///
/// An aggregate root is the single entry point for all mutations within its
/// consistency boundary. External code must always interact with the aggregate
/// through its public methods — never by reaching in and modifying child
/// entities directly.
///
/// Domain events raised inside aggregate methods (via <see cref="RaiseDomainEvent"/>)
/// are collected here and published after the unit of work commits, ensuring
/// that events are only dispatched when the state change has been persisted.
/// <c>DomainEventBehavior</c> in the MediatR pipeline is responsible for
/// flushing the queue after <c>SaveChangesAsync</c>.
/// </summary>
/// <typeparam name="TId">The type of the aggregate's identity (e.g. <see cref="Guid"/>).</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Domain events raised during the current unit of work, in the order they occurred.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Parameterless constructor required by EF Core and serializers.</summary>
    protected AggregateRoot() { }

    /// <summary>Creates an aggregate with a known identity.</summary>
    /// <param name="id">The aggregate's unique identifier.</param>
    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>
    /// Appends a domain event to the in-memory queue.
    /// Called from within aggregate mutation methods — never from outside.
    /// </summary>
    /// <param name="domainEvent">The event describing what just happened.</param>
    protected void RaiseDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    /// <summary>
    /// Removes all queued domain events.
    /// Called by <c>DomainEventBehavior</c> after all events have been published,
    /// and by test code that wants to inspect only the events from a specific operation.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
