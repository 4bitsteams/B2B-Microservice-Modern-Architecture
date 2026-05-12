namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Abstraction over a bounded, backpressure-aware in-process message channel.
///
/// PURPOSE
/// ───────
/// Decouples producers (e.g. API controllers, event handlers) from consumers
/// (background processing services) via a fast, non-blocking write path:
///
///   Producer          Channel (bounded)     Consumer
///   ────────          ─────────────────     ────────
///   WriteAsync ──────►  [msg][msg][msg]  ──► ProcessMessageAsync
///                        (capacity cap)       (parallel, N workers)
///
/// Producers do not block on downstream processing. When the channel reaches
/// <c>BoundedCapacity</c>, producers experience backpressure proportional to
/// consumer throughput (<see cref="WriteAsync"/> awaits a free slot) or they can
/// use <see cref="TryWrite"/> to detect fullness without blocking.
///
/// USAGE — producer side
/// ─────────────────────
/// <code>
/// // Fast path — non-blocking, returns false if channel is full.
/// if (!channel.TryWrite(notification))
///     logger.LogWarning("Notification channel full — dropping message");
///
/// // Backpressure-aware path — awaits a free slot.
/// await channel.WriteAsync(notification, ct);
/// </code>
///
/// USAGE — consumer side (background service)
/// ───────────────────────────────────────────
/// <code>
/// await foreach (var msg in channel.ReadAllAsync(stoppingToken))
///     await ProcessAsync(msg, stoppingToken);
/// </code>
///
/// SOLID
/// ─────
/// ISP — narrow interface with only what producers and consumers need.
/// DIP — Application-layer producers depend on this Core abstraction;
///       Infrastructure (<see cref="B2B.Shared.Infrastructure.Channels.BoundedMessageChannel{TMessage}"/>)
///       provides the <c>System.Threading.Channels.Channel&lt;T&gt;</c> implementation.
/// </summary>
/// <typeparam name="TMessage">The message type exchanged between producers and consumers.</typeparam>
public interface IMessageChannel<TMessage> where TMessage : class
{
    /// <summary>
    /// Asynchronously writes <paramref name="message"/> to the channel.
    /// Awaits a free slot if the channel is currently at capacity (backpressure).
    /// </summary>
    ValueTask WriteAsync(TMessage message, CancellationToken ct = default);

    /// <summary>
    /// Attempts to write <paramref name="message"/> to the channel without blocking.
    /// Returns <see langword="false"/> when the channel is full (<c>FullMode = DropWrite</c>
    /// / <c>DropOldest</c> / <c>DropNewest</c>) or when the writer has been completed.
    /// </summary>
    bool TryWrite(TMessage message);

    /// <summary>
    /// Returns an <see cref="IAsyncEnumerable{T}"/> that yields each message as it
    /// becomes available and completes when the channel is marked done or
    /// <paramref name="ct"/> is cancelled.
    /// </summary>
    IAsyncEnumerable<TMessage> ReadAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Number of messages currently buffered in the channel.
    /// Useful for monitoring / alerting dashboards.
    /// </summary>
    int PendingCount { get; }
}
