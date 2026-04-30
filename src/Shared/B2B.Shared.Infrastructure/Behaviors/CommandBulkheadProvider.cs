using System.Collections.Concurrent;

namespace B2B.Shared.Infrastructure.Behaviors;

/// <summary>
/// Singleton provider that vends one <see cref="SemaphoreSlim"/> per command type.
///
/// Each closed generic instantiation of <see cref="RetryBehavior{TRequest,TResponse}"/>
/// calls <see cref="GetOrCreate{TRequest}"/> exactly once (per instance) to obtain
/// a shared semaphore that caps concurrent executions of that specific command.
///
/// Registration: <see cref="Extensions.ServiceCollectionExtensions.AddMediatRWithBehaviors"/>
/// registers this as a singleton so the semaphore dictionary lives for the lifetime
/// of the application — exactly the scope needed for a bulkhead.
/// </summary>
public sealed class CommandBulkheadProvider
{
    private readonly ConcurrentDictionary<Type, SemaphoreSlim> _semaphores = new();

    /// <summary>
    /// Returns the bulkhead semaphore for <typeparamref name="TRequest"/>, creating it
    /// with <paramref name="maxConcurrency"/> on the first call for that type.
    /// Subsequent calls return the same instance regardless of <paramref name="maxConcurrency"/>.
    /// </summary>
    public SemaphoreSlim GetOrCreate<TRequest>(int maxConcurrency) =>
        _semaphores.GetOrAdd(typeof(TRequest), _ => new SemaphoreSlim(maxConcurrency, maxConcurrency));
}
