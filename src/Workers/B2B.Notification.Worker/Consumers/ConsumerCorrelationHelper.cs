using MassTransit;
using B2B.Shared.Infrastructure.Http;

namespace B2B.Notification.Worker.Consumers;

/// <summary>
/// Restores the originating request's correlation ID from the Kafka message
/// headers so that all log lines emitted during consumer processing carry the
/// same trace ID as the HTTP request that triggered the event.
/// </summary>
internal static class ConsumerCorrelationHelper
{
    internal static string Restore(ConsumeContext context)
    {
        var correlationId = context.Headers.Get<string>(CorrelationIdMiddleware.HeaderName)
                            ?? string.Empty;
        CorrelationIdProvider.SetForCurrentThread(correlationId);
        return correlationId;
    }
}
