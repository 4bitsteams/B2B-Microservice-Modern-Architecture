namespace B2B.Shared.Infrastructure.BackgroundServices;

/// <summary>
/// Runtime-tunable options for <see cref="ConsumerBackgroundService{TMessage}"/>.
///
/// Bind from configuration so thresholds can be adjusted per-environment
/// without a code change or redeployment:
///
/// <code>
/// // appsettings.json
/// {
///   "BackgroundServices": {
///     "OutboxRelay": {
///       "PollingInterval": "00:00:05",
///       "MessageProcessingTimeout": "00:00:30",
///       "BatchSize": 50,
///       "ContinueOnMessageError": true
///     }
///   }
/// }
///
/// // Registration
/// services.Configure&lt;ConsumerBackgroundServiceOptions&gt;(
///     config.GetSection("BackgroundServices:OutboxRelay"));
/// </code>
/// </summary>
public sealed class ConsumerBackgroundServiceOptions
{
    /// <summary>
    /// How long the service waits between poll cycles when no messages were
    /// returned in the previous batch.
    /// Default: 5 seconds. Lower in dev for faster feedback; raise in prod to
    /// reduce DB load when the queue is mostly empty.
    /// </summary>
    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum time allowed for processing a single message before the linked
    /// <see cref="CancellationTokenSource"/> fires and the operation is aborted.
    /// Default: 30 seconds. This guards against handlers that hang on downstream I/O.
    /// </summary>
    public TimeSpan MessageProcessingTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of messages fetched from the source in a single poll cycle.
    /// Limits memory pressure and keeps each batch transactionally small.
    /// Default: 50.
    /// </summary>
    public int BatchSize { get; init; } = 50;

    /// <summary>
    /// When <see langword="true"/> (default), the service logs the error and
    /// continues with the next message after a per-message failure.
    /// When <see langword="false"/>, the exception is re-thrown and the
    /// <see cref="ConsumerBackgroundService{TMessage}"/> stops; the host will
    /// restart it according to the configured restart policy.
    /// </summary>
    public bool ContinueOnMessageError { get; init; } = true;
}
