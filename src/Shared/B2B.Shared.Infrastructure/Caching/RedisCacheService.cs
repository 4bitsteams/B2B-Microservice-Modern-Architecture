using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Caching;

public sealed class RedisCacheService(
    IDistributedCache cache,
    IConnectionMultiplexer multiplexer,
    ILogger<RedisCacheService> logger) : ICacheService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Per-key semaphores prevent cache stampede (thundering herd) on cache miss.
    // Multiple concurrent requests for the same key queue behind the first caller;
    // subsequent requests hit the now-populated cache.
    //
    // MemoryCache with sliding expiration auto-evicts semaphores that haven't been
    // accessed for 5 minutes, preventing unbounded growth when key cardinality is
    // high (e.g. many tenants × many pages × many search terms).
    // SizeLimit caps memory to 10 000 concurrent in-flight distinct keys.
    //
    // NOTE: This guards within a single process. For full multi-replica stampede
    // protection a Redis-based distributed lock (Redlock) would be needed.
    private readonly IMemoryCache _semaphoreCache = new MemoryCache(new MemoryCacheOptions
    {
        SizeLimit = 10_000
    });

    private SemaphoreSlim GetSemaphore(string key) =>
        _semaphoreCache.GetOrCreate(key, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(5);
            entry.Size = 1;
            return new SemaphoreSlim(1, 1);
        })!;

    public void Dispose() => _semaphoreCache.Dispose();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var data = await cache.GetStringAsync(key, ct);
            return data is null ? null : JsonSerializer.Deserialize<T>(data, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache GET failed for key: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(15)
            };
            var data = JsonSerializer.Serialize(value, JsonOptions);
            await cache.SetStringAsync(key, data, options, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache SET failed for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try { await cache.RemoveAsync(key, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Cache REMOVE failed for key: {Key}", key); }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        try
        {
            var db = multiplexer.GetDatabase();
            foreach (var endpoint in multiplexer.GetEndPoints())
            {
                var server = multiplexer.GetServer(endpoint);
                // SCAN is non-blocking; preferred over KEYS in production.
                var keys = server.Keys(pattern: $"{prefix}*", pageSize: 250).ToArray();
                if (keys.Length > 0)
                    await db.KeyDeleteAsync(keys);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache REMOVE BY PREFIX failed for prefix: {Prefix}", prefix);
        }
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key, Func<Task<T>> factory,
        TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        // Fast path — no locking needed if value is already cached.
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null) return cached;

        // Slow path — acquire per-key semaphore to prevent stampede.
        var semaphore = GetSemaphore(key);
        await semaphore.WaitAsync(ct);
        try
        {
            // Double-check: a previous waiter may have already populated the cache.
            cached = await GetAsync<T>(key, ct);
            if (cached is not null) return cached;

            var value = await factory();
            if (value is not null)
                await SetAsync(key, value, expiry, ct);
            return value!;
        }
        finally
        {
            semaphore.Release();
        }
    }
}
