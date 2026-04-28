namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Provides the CorrelationId for the current execution context.
///
/// Works in both HTTP request contexts (reads from HttpContext) and
/// message-consumer contexts (reads from AsyncLocal set by the consumer filter).
/// Inject this wherever you need to tag logs, outgoing messages, or audit records
/// with the trace identifier that ties a distributed operation together.
/// </summary>
public interface ICorrelationIdProvider
{
    /// <summary>
    /// The current CorrelationId.  Returns <see cref="string.Empty"/> when no
    /// correlation context has been established (e.g., during startup).
    /// </summary>
    string CorrelationId { get; }
}
