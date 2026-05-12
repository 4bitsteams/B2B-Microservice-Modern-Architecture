using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Infrastructure.Http;
// CorrelationIdProvider.SetForCurrentThread is the static setter used by consumers
// outside an HTTP context to stamp AsyncLocal so ICorrelationIdProvider returns the
// originating request's trace ID during background processing.

namespace B2B.Shared.Infrastructure.BackgroundServices;

/// <summary>
/// Abstract base class for poll-based background consumers that fetch a batch of
/// messages from any source (database outbox, Redis queue, file system, etc.),
/// process each one in isolation, and then sleep until the next poll cycle.
///
/// DESIGN (SOLID)
/// ──────────────
/// S — Single Responsibility: this class owns the poll loop, linked-token lifecycle,
///     error isolation, and metrics. Subclasses own only FetchMessagesAsync and
///     ProcessMessageAsync — the domain logic.
/// O — Open/Closed: new consumers extend this class without modifying it.
/// L — Substitutable with any IHostedService — callers interact via the DI container.
/// D — Depends on IServiceScopeFactory (DI abstraction), ILogger abstraction, and
///     IErrorMetricsService abstraction. No Infrastructure types in the public API.
///
/// CANCELLATION TOKEN SAFETY (3-layer model)
/// ──────────────────────────────────────────
/// Layer 1 — Host shutdown token (<c>stoppingToken</c>):
///   Passed to every method. When the host signals shutdown, the current poll cycle
///   is completed gracefully; no new cycle is started; the loop exits.
///
/// Layer 2 — Batch-fetch timeout (linked token, <see cref="ConsumerBackgroundServiceOptions.PollingInterval"/> × 3):
///   A linked CancellationTokenSource combines stoppingToken with a maximum fetch
///   duration so a slow DB query cannot block the service from shutting down.
///
/// Layer 3 — Per-message processing timeout (<see cref="ConsumerBackgroundServiceOptions.MessageProcessingTimeout"/>):
///   Each message gets its own linked CancellationTokenSource. If processing a
///   single message exceeds the timeout, the token fires, the I/O is cancelled,
///   an error metric is recorded, and (if ContinueOnMessageError) processing moves
///   to the next message. This prevents one misbehaving message from starving the queue.
///
/// SCOPED SERVICE ACCESS
/// ─────────────────────
/// BackgroundService is registered as a Singleton. Repositories, DbContexts, and most
/// application services are Scoped. Injecting them directly would be a captive-dependency
/// bug. This base class uses <see cref="IServiceScopeFactory"/> to create a fresh DI
/// scope for each batch fetch and for each message, matching the expected service lifetime:
///
///   • FetchMessagesAsync → one scope per poll cycle (shared context across the fetch)
///   • ProcessMessageAsync → one scope per message (isolated unit of work)
///
/// CORRELATION ID PROPAGATION
/// ──────────────────────────
/// Each message processing scope restores the correlation ID from the message (if
/// available via the <see cref="GetCorrelationId"/> override) into the thread-local
/// <see cref="ICorrelationIdProvider"/> so all logs and spans emitted during
/// processing carry the originating request's trace ID — enabling end-to-end
/// distributed tracing from HTTP request → Kafka message → background processing.
/// </summary>
/// <typeparam name="TMessage">
/// The message type consumed from the source. Can be any POCO record or class.
/// </typeparam>
public abstract class ConsumerBackgroundService<TMessage> : BackgroundService
    where TMessage : class
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly ConsumerBackgroundServiceOptions _options;

    // Computed once — avoids per-cycle string allocation on the hot path.
    private readonly string _serviceName;

    protected ConsumerBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        IOptions<ConsumerBackgroundServiceOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _options      = options.Value;
        _serviceName  = GetType().Name;
    }

    // ── BackgroundService.ExecuteAsync ───────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Consumer started: {ServiceName}", _serviceName);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processedCount = await RunBatchAsync(stoppingToken);

            // When the batch was empty, apply the full polling interval so we
            // do not spin at 100 % CPU on an empty queue.
            // When we processed a full batch, skip the delay so we immediately
            // drain the remaining queue before sleeping.
            var delay = processedCount < _options.BatchSize
                ? _options.PollingInterval
                : TimeSpan.Zero;

            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break; // Host is shutting down — exit immediately.
                }
            }
        }

        _logger.LogInformation("Consumer stopped: {ServiceName}", _serviceName);
    }

    // ── Batch execution ───────────────────────────────────────────────────────

    /// <summary>Returns the number of messages successfully fetched and attempted.</summary>
    private async Task<int> RunBatchAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<TMessage> messages;

        // ── Layer 2: batch-fetch timeout ─────────────────────────────────────
        // Fetch timeout = 3× polling interval so a slow DB never blocks shutdown.
        using var fetchCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        fetchCts.CancelAfter(_options.PollingInterval * 3);

        try
        {
            await using var fetchScope = _scopeFactory.CreateAsyncScope();
            messages = await FetchMessagesAsync(fetchScope.ServiceProvider, fetchCts.Token);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return 0; // Normal shutdown — exit silently.
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Fetch timed out in {ServiceName}", _serviceName);
            return 0;
        }
        catch (Exception ex)
        {
            await using var errScope = _scopeFactory.CreateAsyncScope();
            errScope.ServiceProvider
                .GetRequiredService<IErrorMetricsService>()
                .RecordUnhandledException(ex.GetType().Name, _serviceName);

            _logger.LogError(ex, "Batch fetch failed in {ServiceName}", _serviceName);
            return 0;
        }

        if (messages.Count == 0) return 0;

        _logger.LogDebug("{ServiceName} fetched {Count} message(s)", _serviceName, messages.Count);

        // ── Per-message processing loop ───────────────────────────────────────
        foreach (var message in messages)
        {
            if (stoppingToken.IsCancellationRequested) break;
            await ProcessOneAsync(message, stoppingToken);
        }

        return messages.Count;
    }

    private async Task ProcessOneAsync(TMessage message, CancellationToken stoppingToken)
    {
        // ── Layer 3: per-message timeout (linked to host-shutdown token) ──────
        using var messageCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        messageCts.CancelAfter(_options.MessageProcessingTimeout);

        // Restore correlation ID so all log lines within processing carry the
        // originating request's trace ID for end-to-end observability.
        var correlationId = GetCorrelationId(message);

        try
        {
            await using var messageScope = _scopeFactory.CreateAsyncScope();

            // Stamp the AsyncLocal so ICorrelationIdProvider returns the originating
            // request's trace ID for all log/span lines emitted during processing.
            if (!string.IsNullOrEmpty(correlationId))
                CorrelationIdProvider.SetForCurrentThread(correlationId);

            await ProcessMessageAsync(message, messageScope.ServiceProvider, messageCts.Token);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down — rethrow so the outer loop exits.
            throw;
        }
        catch (OperationCanceledException)
        {
            // Per-message timeout fired.
            await using var errScope = _scopeFactory.CreateAsyncScope();
            errScope.ServiceProvider
                .GetRequiredService<IErrorMetricsService>()
                .RecordUnhandledException("MessageProcessingTimeout", typeof(TMessage).Name);

            _logger.LogWarning(
                "Message processing timed out ({Timeout}s) in {ServiceName} | CorrelationId: {CorrelationId}",
                _options.MessageProcessingTimeout.TotalSeconds, _serviceName, correlationId);

            if (!_options.ContinueOnMessageError)
                throw;
        }
        catch (Exception ex)
        {
            await using var errScope = _scopeFactory.CreateAsyncScope();
            errScope.ServiceProvider
                .GetRequiredService<IErrorMetricsService>()
                .RecordUnhandledException(ex.GetType().Name, typeof(TMessage).Name);

            _logger.LogError(ex,
                "Error processing {MessageType} in {ServiceName} | CorrelationId: {CorrelationId}",
                typeof(TMessage).Name, _serviceName, correlationId);

            if (!_options.ContinueOnMessageError)
                throw;
        }
    }

    // ── Abstract members for subclasses to implement ─────────────────────────

    /// <summary>
    /// Fetches up to <see cref="ConsumerBackgroundServiceOptions.BatchSize"/> messages
    /// from the source.
    ///
    /// The <paramref name="services"/> scope is shared across the entire batch fetch
    /// so callers can reuse a single DbContext, Redis connection, etc.
    ///
    /// Return an empty list to signal "no work available" — the service will
    /// sleep for <see cref="ConsumerBackgroundServiceOptions.PollingInterval"/> before retrying.
    /// </summary>
    protected abstract Task<IReadOnlyList<TMessage>> FetchMessagesAsync(
        IServiceProvider services, CancellationToken ct);

    /// <summary>
    /// Processes a single <paramref name="message"/> in isolation.
    ///
    /// The <paramref name="services"/> scope is unique per message — inject a fresh
    /// DbContext, IUnitOfWork, IEventBus, etc. directly from <paramref name="services"/>.
    /// Each message gets a separate unit of work so a failure in one message does
    /// not roll back the others.
    /// </summary>
    protected abstract Task ProcessMessageAsync(
        TMessage message, IServiceProvider services, CancellationToken ct);

    /// <summary>
    /// Override to extract the correlation ID from the message so log lines
    /// emitted during processing carry the originating request's trace ID.
    ///
    /// Default implementation returns <see langword="null"/> (no correlation).
    /// </summary>
    protected virtual string? GetCorrelationId(TMessage message) => null;
}
