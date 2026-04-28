namespace B2B.Shared.Core.CQRS;

/// <summary>
/// Opt-in interface that marks a command as idempotent.
/// Commands implementing this interface carry a client-supplied
/// <see cref="IdempotencyKey"/> that <c>IdempotencyBehavior</c> uses to
/// detect and short-circuit duplicate requests, returning the cached
/// response instead of re-executing the handler.
///
/// <para>
/// Use for operations that must be safe to retry — e.g. payment commands,
/// order placement, or any command dispatched over an unreliable transport.
/// </para>
/// </summary>
public interface IIdempotentCommand
{
    /// <summary>
    /// A client-generated unique key (e.g. a UUID v4) that identifies this
    /// specific invocation. Two requests with the same key are treated as
    /// duplicates: the second returns the first's cached response without
    /// re-running the handler.
    /// </summary>
    string IdempotencyKey { get; }
}
