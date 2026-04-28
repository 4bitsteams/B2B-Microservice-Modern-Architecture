namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Abstraction for distributed cache operations (Cache-Aside pattern).
///
/// Application handlers depend on this interface; the Infrastructure layer
/// provides the concrete implementation (<c>RedisCacheService</c> backed by
/// StackExchange.Redis). Swap with an in-memory implementation for unit tests.
///
/// All keys are tenant-scoped by convention to prevent cross-tenant data leakage:
/// <code>
/// $"products:tenant:{tenantId}:page:{page}"
/// </code>
///
/// Invalidate on write commands by removing the matching prefix:
/// <code>
/// await cache.RemoveByPrefixAsync($"products:tenant:{tenantId}");
/// </code>
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or <see langword="null"/>
    /// on a cache miss.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="key"/> with an optional
    /// sliding or absolute <paramref name="expiry"/>.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;

    /// <summary>Removes the entry for <paramref name="key"/> if it exists.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Removes all entries whose keys begin with <paramref name="prefix"/>.
    /// Use to invalidate an entire tenant's cached collection after a write.
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>
    /// Returns the cached value for <paramref name="key"/>; on a miss, invokes
    /// <paramref name="factory"/> to load the value, stores it under the key,
    /// and returns it. Subsequent calls within the <paramref name="expiry"/> window
    /// return the cached value without hitting the database.
    /// </summary>
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;
}
