using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Messaging;

/// <summary>
/// <see cref="IEventBus"/> implementation backed by MassTransit + Apache Kafka.
///
/// Publishing strategy (tried in order):
///   1. <c>ITopicProducer&lt;T&gt;</c> — resolved from DI when the service registers a
///      Kafka producer for <typeparamref name="T"/> via <c>AddEventBus(configureRider:)</c>.
///      This is the primary path for cross-service integration events.
///   2. <c>IBus.Publish</c> — in-memory fallback used by sagas and same-process
///      consumers that do not need Kafka transport.
///
/// Every published message automatically carries the active <c>X-Correlation-ID</c>
/// as a MassTransit header so the value flows across service boundaries without
/// requiring an explicit field on every message contract.
///
/// Consumers extract the header via:
/// <code>var correlationId = context.Headers.Get&lt;string&gt;("X-Correlation-ID");</code>
/// </summary>
public sealed class MassTransitEventBus(
    IBus bus,
    IServiceProvider serviceProvider,
    ICorrelationIdProvider correlationIdProvider,
    ILogger<MassTransitEventBus> logger) : IEventBus
{
    public async Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
    {
        var correlationId = correlationIdProvider.CorrelationId;

        logger.LogInformation(
            "Publishing {EventType} [{CorrelationId}]", typeof(T).Name, correlationId);

        // Prefer Kafka topic producer when registered (cross-service delivery)
        var producer = serviceProvider.GetService<ITopicProducer<T>>();
        if (producer is not null)
        {
            await producer.Produce(message, Pipe.Execute<KafkaSendContext<T>>(ctx =>
            {
                if (!string.IsNullOrEmpty(correlationId))
                    ctx.Headers.Set("X-Correlation-ID", correlationId);
            }), ct);
            return;
        }

        // Fallback: in-memory bus (sagas, same-process consumers, services without producers)
        await bus.Publish(message, ctx =>
        {
            if (!string.IsNullOrEmpty(correlationId))
                ctx.Headers.Set("X-Correlation-ID", correlationId);
        }, ct);
    }

    public async Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken ct = default) where T : class
        => await PublishAsync(message, ct);
}
