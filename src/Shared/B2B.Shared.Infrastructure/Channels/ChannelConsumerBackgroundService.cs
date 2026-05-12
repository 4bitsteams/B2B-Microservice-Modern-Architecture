using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Channels;

/// <summary>
/// Abstract base class for high-throughput channel-based consumers that process
/// messages written into an <see cref="IMessageChannel{TMessage}"/> by producers.
///
/// DESIGN (SOLID)
/// ──────────────
/// S — Single Responsibility: this class owns the fan-out loop, cancellation lifecycle,
///     error isolation, and metrics. Subclasses own only <see cref="ProcessMessageAsync"/>.
/// O — Open/Closed: new channel consumers extend this class without modifying it.
/// D — Depends on <see cref="IMessageChannel{TMessage}"/> (abstraction), not on
///     <c>System.Threading.Channels</c> directly; and on <see cref="IServiceScopeFactory"/>
///     for scoped service resolution.
///
/// MEMORY AND THROUGHPUT MODEL
/// ───────────────────────────
/// 1. Producer writes to the bounded channel (non-blocking on the hot HTTP path).
/// 2. <see cref="IMessageChannel{TMessage}.ReadAllAsync"/> streams messages out as they arrive.
/// 3. <c>Parallel.ForEachAsync</c> fans out to up to <see cref="ChannelConsumerOptions.MaxConcurrency"/>
///    concurrent workers — each gets its own DI scope (fresh DbContext, IUnitOfWork, etc.).
/// 4. Backpressure flows naturally: when all workers are busy, the channel buffer absorbs
///    bursts up to <see cref="ChannelConsumerOptions.BoundedCapacity"/>; beyond that the
///    producer's <c>WriteAsync</c> awaits a free slot.
///
/// COMPARED TO ConsumerBackgroundService (poll-based)
/// ───────────────────────────────────────────────────
/// • No polling interval — messages are processed as soon as they are written.
/// • No DB round-trip to fetch messages — lower latency for in-process event flows.
/// • True fan-out parallelism via <c>Parallel.ForEachAsync</c> instead of a sequential loop.
///
/// Use <see cref="ChannelConsumerBackgroundService{TMessage}"/> for:
///   • Intra-process notification dispatch, audit writes, telemetry flushing.
///   • Any case where sub-millisecond producer response time matters more than ordering.
///
/// Use <see cref="B2B.Shared.Infrastructure.BackgroundServices.ConsumerBackgroundService{TMessage}"/>
/// for durable outbox relay where messages must survive process restarts.
///
/// SCOPED SERVICE ACCESS
/// ─────────────────────
/// Each message processing invocation creates a fresh DI scope so Scoped services
/// (DbContext, IUnitOfWork, repositories) have the correct lifetime relative to the
/// unit of work, and failures in one message do not affect others.
///
/// CONFIGURATION (via <see cref="ChannelConsumerOptions"/>)
/// ─────────────────────────────────────────────────────────
/// <code>
/// "Channels": {
///   "Notification": {
///     "BoundedCapacity": 2000,
///     "FullMode": "Wait",
///     "MaxConcurrency": 8
///   }
/// }
/// </code>
/// </summary>
/// <typeparam name="TMessage">The message type consumed from the channel.</typeparam>
public abstract class ChannelConsumerBackgroundService<TMessage>(
    IMessageChannel<TMessage> channel,
    IServiceScopeFactory scopeFactory,
    ILogger logger,
    IOptions<ChannelConsumerOptions> options)
    : BackgroundService
    where TMessage : class
{
    private readonly ChannelConsumerOptions _options = options.Value;
    private readonly string _serviceName = typeof(TMessage).Name + "ChannelConsumer";

    // ── BackgroundService.ExecuteAsync ────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Channel consumer started: {ServiceName}", _serviceName);

        try
        {
            // Parallel.ForEachAsync reads messages from the IAsyncEnumerable returned by
            // ReadAllAsync and dispatches each to a worker task, capping concurrency at
            // MaxConcurrency. Workers run on the thread pool — no dedicated threads consumed.
            await Parallel.ForEachAsync(
                channel.ReadAllAsync(stoppingToken),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = _options.MaxConcurrency,
                    CancellationToken      = stoppingToken
                },
                ProcessOneAsync);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown — exit silently.
        }

        logger.LogInformation("Channel consumer stopped: {ServiceName}", _serviceName);
    }

    // ── Per-message worker ────────────────────────────────────────────────────

    private async ValueTask ProcessOneAsync(TMessage message, CancellationToken ct)
    {
        try
        {
            // Fresh scope per message: isolates DbContext, IUnitOfWork, and other
            // Scoped services so a failure in one message does not corrupt another.
            await using var scope = scopeFactory.CreateAsyncScope();
            await ProcessMessageAsync(message, scope.ServiceProvider, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host is shutting down — rethrow so Parallel.ForEachAsync stops cleanly.
            throw;
        }
        catch (Exception ex)
        {
            // Record the metric in its own scope so a broken scope from the failure
            // above does not prevent telemetry from being emitted.
            await using var errScope = scopeFactory.CreateAsyncScope();
            errScope.ServiceProvider
                .GetRequiredService<IErrorMetricsService>()
                .RecordUnhandledException(ex.GetType().Name, typeof(TMessage).Name);

            logger.LogError(ex,
                "Error processing {MessageType} in {ServiceName}",
                typeof(TMessage).Name, _serviceName);

            // Do NOT rethrow — one failed message must not stop the consumer loop.
            // The channel retains unconsumed messages so the next ReadAllAsync iteration
            // continues with the next message.
        }
    }

    // ── Abstract member for subclasses ────────────────────────────────────────

    /// <summary>
    /// Processes a single <paramref name="message"/> using services from the provided
    /// <paramref name="services"/> scope.
    ///
    /// The scope is unique per message — resolve a fresh DbContext, IUnitOfWork,
    /// and repositories directly from <paramref name="services"/>. Each message is
    /// processed in its own unit of work so a failure does not affect neighbours.
    ///
    /// Throwing from this method logs the error and records a metric, but does NOT
    /// stop the consumer loop — the next message will be processed normally.
    /// </summary>
    protected abstract Task ProcessMessageAsync(
        TMessage message, IServiceProvider services, CancellationToken ct);
}
