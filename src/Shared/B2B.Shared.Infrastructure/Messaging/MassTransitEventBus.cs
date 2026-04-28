using MassTransit;
using Microsoft.Extensions.Logging;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Messaging;

/// <summary>
/// <see cref="IEventBus"/> implementation backed by MassTransit.
///
/// Every published message automatically carries the active <c>X-Correlation-ID</c>
/// as a MassTransit header so the value flows across service boundaries without
/// requiring an explicit field on every message contract.
///
/// Consumers extract the header via:
/// <code>var correlationId = context.Headers.Get&lt;string&gt;("X-Correlation-ID");</code>
/// </summary>
public sealed class MassTransitEventBus(
    IPublishEndpoint publishEndpoint,
    ICorrelationIdProvider correlationIdProvider,
    ILogger<MassTransitEventBus> logger) : IEventBus
{
    public async Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
    {
        var correlationId = correlationIdProvider.CorrelationId;

        logger.LogInformation(
            "Publishing {EventType} [{CorrelationId}]", typeof(T).Name, correlationId);

        await publishEndpoint.Publish(message, ctx =>
        {
            if (!string.IsNullOrEmpty(correlationId))
                ctx.Headers.Set("X-Correlation-ID", correlationId);
        }, ct);
    }

    public async Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken ct = default) where T : class
    {
        var correlationId = correlationIdProvider.CorrelationId;

        logger.LogInformation(
            "Publishing {EventType} with routing key {RoutingKey} [{CorrelationId}]",
            typeof(T).Name, routingKey, correlationId);

        await publishEndpoint.Publish(message, ctx =>
        {
            if (!string.IsNullOrEmpty(correlationId))
                ctx.Headers.Set("X-Correlation-ID", correlationId);
        }, ct);
    }
}
