using System.Threading.Channels;
using Microsoft.Extensions.Options;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Channels;

/// <summary>
/// <see cref="System.Threading.Channels.Channel{T}"/>-backed implementation of
/// <see cref="IMessageChannel{TMessage}"/>.
///
/// MEMORY CHARACTERISTICS
/// ──────────────────────
/// • Allocations per write: zero (the message object is the only allocation and is
///   owned by the caller).
/// • The internal channel queue holds references, not copies — no serialisation overhead.
/// • <see cref="BoundedChannelOptions.BoundedCapacity"/> caps the number of in-flight
///   message references to bound memory under high-throughput bursts.
///
/// CONCURRENCY
/// ───────────
/// • <c>SingleWriter = false</c>: multiple HTTP-request threads can call <see cref="WriteAsync"/>
///   concurrently without external locking.
/// • <c>SingleReader = false</c>: <see cref="ChannelConsumerBackgroundService{TMessage}"/> fans
///   out to <c>MaxConcurrency</c> readers via <c>Parallel.ForEachAsync</c>.
/// • <c>AllowSynchronousContinuations = false</c>: continuations always post to the thread
///   pool, preventing unintended inline execution and stack overflows under high load.
///
/// LIFECYCLE
/// ─────────
/// The channel writer is never explicitly completed in normal operation — the consumer
/// background service stops by honouring the <c>stoppingToken</c> passed to
/// <see cref="IMessageChannel{TMessage}.ReadAllAsync"/>. This means the channel can
/// survive service restarts (hosted-service restart policy) without re-registration.
///
/// Register as <b>Singleton</b>: the channel must outlive individual DI scopes so
/// producers and the consumer background service share the same instance.
/// </summary>
public sealed class BoundedMessageChannel<TMessage>(IOptions<ChannelConsumerOptions> options)
    : IMessageChannel<TMessage>
    where TMessage : class
{
    private readonly Channel<TMessage> _channel = Channel.CreateBounded<TMessage>(
        new BoundedChannelOptions(options.Value.BoundedCapacity)
        {
            FullMode                    = options.Value.FullMode,
            SingleWriter                = false,
            SingleReader                = false,
            AllowSynchronousContinuations = false
        });

    /// <inheritdoc/>
    public ValueTask WriteAsync(TMessage message, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(message, ct);

    /// <inheritdoc/>
    public bool TryWrite(TMessage message) =>
        _channel.Writer.TryWrite(message);

    /// <inheritdoc/>
    public IAsyncEnumerable<TMessage> ReadAllAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);

    /// <inheritdoc/>
    public int PendingCount => _channel.Reader.Count;
}
