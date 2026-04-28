namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Abstraction for publishing integration events across service boundaries via
/// the message broker (RabbitMQ through MassTransit).
///
/// Integration events differ from domain events: they cross process boundaries
/// and are serialized over the wire, so they must be stable, versioned contracts
/// (defined in <c>B2B.Shared.Core.Messaging</c>). Handlers subscribe in
/// separate microservices or workers.
///
/// The Infrastructure implementation wraps MassTransit's <c>IPublishEndpoint</c>,
/// which applies the configured outbox policy to guarantee at-least-once delivery.
///
/// Usage from a domain event handler:
/// <code>
/// await eventBus.PublishAsync(new OrderConfirmedIntegration(
///     order.Id, order.OrderNumber, order.CustomerId));
/// </code>
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes <paramref name="message"/> to the default exchange for its type.
    /// </summary>
    /// <typeparam name="T">The integration event type.</typeparam>
    Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Publishes <paramref name="message"/> with an optional <paramref name="routingKey"/>
    /// for topic-based or direct exchange routing.
    /// </summary>
    /// <typeparam name="T">The integration event type.</typeparam>
    /// <param name="routingKey">Optional RabbitMQ routing key. Pass <see langword="null"/> for fanout semantics.</param>
    Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken ct = default) where T : class;
}
