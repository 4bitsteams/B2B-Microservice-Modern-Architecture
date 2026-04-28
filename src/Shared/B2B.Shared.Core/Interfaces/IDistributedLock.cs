namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Distributed mutual exclusion lock backed by Redis.
/// Prevents concurrent processing of the same resource across multiple
/// service instances — critical for inventory reservation, payment processing,
/// and any "check-then-act" pattern under high concurrency.
/// </summary>
public interface IDistributedLock
{
    /// <summary>
    /// Acquires an exclusive lock on <paramref name="resource"/> and returns
    /// a handle that releases the lock on disposal.
    /// </summary>
    /// <param name="resource">Unique name for the protected resource (e.g. "inventory:product:{id}").</param>
    /// <param name="expiry">How long the lock is held before Redis auto-expires it (safety net for crashes).</param>
    /// <param name="wait">How long to keep retrying before giving up.</param>
    /// <param name="retry">Interval between retry attempts.</param>
    /// <returns>An <see cref="ILockHandle"/> whose Dispose releases the lock, or null if the lock could not be acquired.</returns>
    Task<ILockHandle?> AcquireAsync(
        string resource,
        TimeSpan expiry,
        TimeSpan wait,
        TimeSpan retry,
        CancellationToken ct = default);
}

/// <summary>Represents a held distributed lock. Dispose releases the lock.</summary>
public interface ILockHandle : IAsyncDisposable
{
    bool IsAcquired { get; }
}
