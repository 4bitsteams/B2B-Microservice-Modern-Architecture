namespace B2B.Shared.Infrastructure.BackgroundServices;

/// <summary>
/// Persistent record of an integration event that must be relayed to the event bus.
///
/// PURPOSE
/// ───────
/// The outbox pattern guarantees at-least-once delivery of integration events even
/// when the event bus (Kafka) is temporarily unavailable:
///   1. A handler writes its domain changes AND an OutboxMessage in the same local
///      database transaction (atomicity — both succeed or both fail together).
///   2. <see cref="OutboxRelayBackgroundService"/> polls for unprocessed records and
///      publishes them to the event bus, then marks them as processed.
///   3. If the process crashes after publishing but before marking as processed,
///      the relay will publish again on restart — consumers must be idempotent.
///
/// This record type is intentionally an Infrastructure concern: it has no domain
/// meaning and exists only to bridge the gap between the local DB transaction and
/// the distributed message bus.
///
/// CONCURRENCY SAFETY
/// ──────────────────
/// <see cref="OutboxRelayBackgroundService"/> uses
/// <see cref="B2B.Shared.Core.Interfaces.ILockableRepository{TEntity,TId}"/> style
/// locking when fetching pending rows to prevent multiple relay instances from
/// processing the same message concurrently (e.g. when scaled horizontally).
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Stable identifier — used as the idempotency key when publishing.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Assembly-qualified CLR type name of the integration event payload.
    /// Used by <see cref="OutboxRelayBackgroundService"/> to deserialise
    /// <see cref="Payload"/> back to the correct concrete type before publishing.
    /// </summary>
    public string MessageType { get; init; } = string.Empty;

    /// <summary>JSON-serialised integration event payload.</summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>
    /// Optional correlation ID copied from the originating HTTP request so
    /// relay log lines carry the same trace ID as the handler that created
    /// the outbox record.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>UTC timestamp when the record was inserted.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the message was successfully published to the event bus.
    /// <see langword="null"/> while the record is pending.
    /// </summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>
    /// Description of the last error that prevented processing.
    /// <see langword="null"/> on the success path.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Number of times processing has been attempted and failed.
    /// Used to implement dead-letter logic: stop retrying after N failures.
    /// </summary>
    public int RetryCount { get; private set; }

    // ── State transitions ─────────────────────────────────────────────────────

    /// <summary>
    /// Marks this record as successfully published.
    /// Called by <see cref="OutboxRelayBackgroundService"/> after a confirmed
    /// delivery to the event bus.
    /// </summary>
    public void MarkProcessed() => ProcessedAt = DateTime.UtcNow;

    /// <summary>
    /// Records a delivery failure and increments the retry counter.
    /// The relay will retry on the next poll cycle unless
    /// <see cref="RetryCount"/> exceeds the configured maximum.
    /// </summary>
    public void MarkFailed(string error)
    {
        Error      = error;
        RetryCount++;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="OutboxMessage"/> from an integration event.
    /// Call this inside a command handler within the same EF Core unit of work
    /// that persists the domain mutation so both writes are atomic.
    /// </summary>
    /// <typeparam name="TEvent">The integration event type.</typeparam>
    /// <param name="event">The event to serialise into the outbox.</param>
    /// <param name="correlationId">
    /// The current request's correlation ID. Pass
    /// <see cref="B2B.Shared.Core.Interfaces.ICorrelationIdProvider.CorrelationId"/>
    /// so the relay can restore the trace context when processing.
    /// </param>
    public static OutboxMessage From<TEvent>(TEvent @event, string? correlationId = null)
        where TEvent : class
    {
        return new OutboxMessage
        {
            MessageType   = typeof(TEvent).AssemblyQualifiedName
                            ?? typeof(TEvent).FullName
                            ?? typeof(TEvent).Name,
            Payload       = System.Text.Json.JsonSerializer.Serialize(@event),
            CorrelationId = correlationId
        };
    }
}
