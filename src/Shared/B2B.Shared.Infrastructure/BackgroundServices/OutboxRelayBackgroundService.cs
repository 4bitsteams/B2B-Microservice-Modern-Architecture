using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.BackgroundServices;

/// <summary>
/// Concrete <see cref="ConsumerBackgroundService{TMessage}"/> that relays
/// <see cref="OutboxMessage"/> records from the database to the event bus.
///
/// OUTBOX RELAY FLOW
/// ─────────────────
///   1. Poll: SELECT ... FOR UPDATE SKIP LOCKED — fetch up to BatchSize pending
///      outbox rows, locking them at the DB level so only ONE relay instance
///      processes each record (safe for horizontal scaling).
///   2. Deserialise: reconstruct the original integration event from the JSON payload.
///   3. Publish: forward the event to the event bus via IEventBus.PublishAsync.
///   4. Mark processed: set ProcessedAt = UtcNow within the same transaction
///      that held the lock so there is no window for double-processing.
///   5. On failure: call OutboxMessage.MarkFailed, increment RetryCount.
///      Messages exceeding MaxRetryCount are dead-lettered (ProcessedAt stamped
///      with the failure timestamp and Error populated).
///
/// GUARANTEES
/// ──────────
/// • At-least-once delivery: if the process dies after publishing but before
///   marking processed, the relay will publish again on restart.
///   Consumers MUST be idempotent (use the OutboxMessage.Id as an idempotency key).
/// • No double-processing across instances: FOR UPDATE SKIP LOCKED ensures that
///   a row being processed by one relay pod is invisible to all others.
/// • Cancellation-safe: every DB and event-bus call respects the linked token.
///
/// CONFIGURATION (via <see cref="ConsumerBackgroundServiceOptions"/>)
/// ──────────────────────────────────────────────────────────────────
/// Bind from appsettings.json:
/// <code>
/// "BackgroundServices": {
///   "OutboxRelay": {
///     "PollingInterval": "00:00:05",
///     "MessageProcessingTimeout": "00:00:30",
///     "BatchSize": 50,
///     "ContinueOnMessageError": true
///   }
/// }
/// </code>
///
/// USAGE — Register in each microservice that uses the outbox pattern:
/// <code>
/// services.AddOutboxRelay&lt;MyDbContext&gt;(config);
/// </code>
/// </summary>
public sealed class OutboxRelayBackgroundService<TContext>(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxRelayBackgroundService<TContext>> logger,
    IOptions<ConsumerBackgroundServiceOptions> options)
    : ConsumerBackgroundService<OutboxMessage>(scopeFactory, logger, options)
    where TContext : DbContext
{
    /// <summary>
    /// After this many relay failures for a single message, stop retrying and
    /// dead-letter it so it does not block healthy messages behind it.
    /// </summary>
    private const int MaxRetryCount = 5;

    // ── ConsumerBackgroundService<OutboxMessage> implementation ───────────────

    /// <summary>
    /// Fetches up to <see cref="ConsumerBackgroundServiceOptions.BatchSize"/> unprocessed
    /// outbox messages using <c>FOR UPDATE SKIP LOCKED</c>.
    ///
    /// <c>SKIP LOCKED</c> skips rows locked by other relay instances (horizontal
    /// scaling) so each pod works on a disjoint subset of the pending queue.
    /// The lock is held until the enclosing transaction commits (inside
    /// <see cref="ProcessMessageAsync"/>).
    /// </summary>
    protected override async Task<IReadOnlyList<OutboxMessage>> FetchMessagesAsync(
        IServiceProvider services, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var ctx = services.GetRequiredService<TContext>();

        // Raw SQL to use FOR UPDATE SKIP LOCKED — not expressible in LINQ.
        // Parameters are positional ($1, $2) so there is no injection risk.
        return await ctx.Set<OutboxMessage>()
            .FromSqlRaw(
                """
                SELECT * FROM "OutboxMessages"
                WHERE  "ProcessedAt" IS NULL
                  AND  "RetryCount"  <  {0}
                ORDER BY "CreatedAt"
                LIMIT  {1}
                FOR UPDATE SKIP LOCKED
                """,
                MaxRetryCount,
                Options.BatchSize)
            .AsTracking()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Deserialises and publishes a single <see cref="OutboxMessage"/> inside a
    /// new database transaction so the lock, the publish confirmation, and the
    /// "mark processed" update are all atomic.
    /// </summary>
    protected override async Task ProcessMessageAsync(
        OutboxMessage message, IServiceProvider services, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var ctx      = services.GetRequiredService<TContext>();
        var eventBus = services.GetRequiredService<IEventBus>();
        var unitOfWork = (IUnitOfWork)ctx;

        await unitOfWork.ExecuteInTransactionAsync(async transactionCt =>
        {
            // Re-attach the outbox record to the new scope's context so EF Core
            // can track the MarkProcessed / MarkFailed mutation.
            ctx.Attach(message);

            try
            {
                var eventType = Type.GetType(message.MessageType)
                    ?? throw new InvalidOperationException(
                        $"Cannot resolve CLR type '{message.MessageType}'. " +
                        "Ensure the assembly is referenced by this host.");

                var payload = JsonSerializer.Deserialize(message.Payload, eventType)
                    ?? throw new InvalidOperationException(
                        $"Deserialisation returned null for outbox message {message.Id}.");

                // Publish to Kafka / in-memory bus via IEventBus.
                // Uses the dynamic dispatch pattern so the generic type argument
                // is the runtime type, not 'object'.
                await eventBus.PublishAsync((dynamic)payload, ct: transactionCt);

                message.MarkProcessed();

                logger.LogInformation(
                    "Outbox message relayed | Id: {MessageId} | Type: {MessageType} | CorrelationId: {CorrelationId}",
                    message.Id, message.MessageType, message.CorrelationId);
            }
            catch (OperationCanceledException)
            {
                // Cancellation (host shutdown or per-message timeout) — do not
                // record a failure; the record stays pending for the next relay run.
                throw;
            }
            catch (Exception ex)
            {
                message.MarkFailed(ex.Message);

                if (message.RetryCount >= MaxRetryCount)
                {
                    logger.LogError(ex,
                        "Outbox message dead-lettered after {RetryCount} attempts | " +
                        "Id: {MessageId} | Type: {MessageType}",
                        message.RetryCount, message.Id, message.MessageType);
                }
                else
                {
                    logger.LogWarning(ex,
                        "Outbox message relay failed (attempt {RetryCount}/{MaxRetries}) | " +
                        "Id: {MessageId} | Type: {MessageType}",
                        message.RetryCount, MaxRetryCount, message.Id, message.MessageType);
                }

                // Let the transaction commit with the MarkFailed state so the
                // retry counter is persisted. Do NOT re-throw; ContinueOnMessageError
                // in the base class governs whether the loop stops.
            }

            return message; // satisfies ExecuteInTransactionAsync<TResult>
        }, ct);
    }

    /// <summary>
    /// Restores the correlation ID from the outbox record into the async execution
    /// context so all log lines during relay carry the originating trace ID.
    /// </summary>
    protected override string? GetCorrelationId(OutboxMessage message) =>
        message.CorrelationId;
}
