namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Abstraction for publishing integration events across service boundaries via
/// Apache Kafka through MassTransit.
///
/// Integration events differ from domain events: they cross process boundaries
/// and are serialized over the wire, so they must be stable, versioned contracts
/// (defined in <c>B2B.Shared.Core.Messaging</c>). Topic names are defined in
/// <see cref="B2B.Shared.Core.Messaging.KafkaTopics"/>. Consumers subscribe in
/// separate microservices or workers via Kafka consumer groups.
///
/// The Infrastructure implementation resolves <c>ITopicProducer&lt;T&gt;</c> at runtime.
/// Services must register producers via <c>AddEventBus(configureRider:)</c> for
/// cross-service delivery. Without a registered producer the message falls back to
/// the in-memory bus (suitable for sagas and same-process communication only).
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
    /// <param name="routingKey">Optional routing hint (unused in Kafka; reserved for future use). Pass <see langword="null"/> for default routing.</param>
    Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken ct = default) where T : class;
}
