using System.Threading.Channels;

namespace B2B.Shared.Infrastructure.Channels;

/// <summary>
/// Runtime-tunable options for <see cref="ChannelConsumerBackgroundService{TMessage}"/>
/// and <see cref="BoundedMessageChannel{TMessage}"/>.
///
/// Bind from configuration per consumer:
/// <code>
/// // appsettings.json
/// {
///   "Channels": {
///     "Notification": {
///       "BoundedCapacity": 2000,
///       "FullMode": "Wait",
///       "MaxConcurrency": 8
///     }
///   }
/// }
///
/// // Registration
/// services.AddMessageChannel&lt;NotificationMessage, NotificationChannelConsumer&gt;(
///     config, "Channels:Notification");
/// </code>
/// </summary>
public sealed class ChannelConsumerOptions
{
    /// <summary>
    /// Maximum number of messages buffered in the channel before backpressure applies.
    /// Larger values smooth out burst traffic at the cost of more memory; smaller values
    /// propagate backpressure to producers sooner.
    /// Default: 1 000 messages.
    /// </summary>
    public int BoundedCapacity { get; init; } = 1_000;

    /// <summary>
    /// Behaviour when the channel is full and a producer calls <c>WriteAsync</c> or <c>TryWrite</c>.
    ///
    /// • <see cref="BoundedChannelFullMode.Wait"/> (default) — <c>WriteAsync</c> awaits a free slot;
    ///   <c>TryWrite</c> returns <see langword="false"/>.
    /// • <see cref="BoundedChannelFullMode.DropWrite"/> — silently discards the new message.
    /// • <see cref="BoundedChannelFullMode.DropNewest"/> — removes the newest buffered message.
    /// • <see cref="BoundedChannelFullMode.DropOldest"/> — removes the oldest buffered message.
    ///
    /// Prefer <c>Wait</c> for notifications (no loss acceptable).
    /// Prefer <c>DropOldest</c> for telemetry/metrics (slight staleness acceptable).
    /// </summary>
    public BoundedChannelFullMode FullMode { get; init; } = BoundedChannelFullMode.Wait;

    /// <summary>
    /// Maximum number of messages processed concurrently by
    /// <see cref="ChannelConsumerBackgroundService{TMessage}"/> via <c>Parallel.ForEachAsync</c>.
    /// Default: <see cref="Environment.ProcessorCount"/> (one worker per logical CPU).
    /// Increase for I/O-bound consumers; keep low for CPU-bound ones.
    /// </summary>
    public int MaxConcurrency { get; init; } = Environment.ProcessorCount;
}
