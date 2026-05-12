using System.Buffers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Caching;

/// <summary>
/// Redis-backed implementation of <see cref="ICacheService"/> with zero-allocation
/// serialisation on the hot GET/SET path.
///
/// MEMORY OPTIMISATIONS
/// ────────────────────
/// <b>GET path (deserialise):</b>
///   <c>IDistributedCache.GetAsync</c> returns <c>byte[]?</c> directly from the Redis wire.
///   <c>JsonSerializer.Deserialize&lt;T&gt;(ReadOnlySpan&lt;byte&gt;)</c> parses directly from the byte
///   span, skipping the UTF-8 → string allocation and the subsequent string → parser
///   re-encoding that the old <c>GetStringAsync</c> path required.
///   Allocation delta vs. the string path: −1 string per cache hit.
///
/// <b>SET path (serialise):</b>
///   <c>ArrayBufferWriter&lt;byte&gt;</c> + <c>Utf8JsonWriter</c> serialise directly to a byte buffer,
///   avoiding the intermediate <c>string</c> and the <c>Encoding.UTF8.GetBytes</c> call that
///   <c>SetStringAsync</c> performed internally.
///   Allocation delta vs. the string path: −1 string per cache write.
///
/// STAMPEDE PROTECTION
/// ───────────────────
/// Per-key <see cref="SemaphoreSlim"/> instances are held in a size-capped
/// <see cref="IMemoryCache"/> (10 000 entries, 5-minute sliding eviction).
/// <see cref="GetOrCreateAsync{T}"/> uses a double-check pattern:
///   1. Fast path — attempt read without acquiring the semaphore.
///   2. Slow path — acquire per-key semaphore, re-check, call factory, store.
/// This prevents the thundering-herd problem within a single process replica.
/// For full multi-replica stampede protection use a Redis-based distributed lock.
/// </summary>
public sealed class RedisCacheService(
    IDistributedCache cache,
    IConnectionMultiplexer multiplexer,
    ILogger<RedisCacheService> logger) : ICacheService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase
    };

    // ── Stampede-guard constants ───────────────────────────────────────────────

    /// <summary>
    /// Maximum number of distinct cache keys whose semaphores can be held in memory
    /// at once. Caps memory under high key-cardinality workloads (many tenants × many
    /// pages). Each semaphore entry costs ~64 bytes; 10 000 entries ≈ 640 KB.
    /// </summary>
    private const int SemaphoreCacheSizeLimit = 10_000;

    /// <summary>
    /// Sliding window after which an idle semaphore is evicted from the MemoryCache.
    /// A key that hasn't been requested for this long no longer needs stampede protection.
    /// </summary>
    private static readonly TimeSpan SemaphoreIdleEvictionWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Default cache TTL applied when the caller does not specify an expiry.
    /// Keeps read-heavy pages fresh without forcing explicit TTL on every call site.
    /// </summary>
    private static readonly TimeSpan DefaultCacheExpiry = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Redis SCAN page size. Larger pages reduce round-trips; 250 is a safe default
    /// that avoids blocking the server for too long on large key spaces.
    /// </summary>
    private const int RedisScanPageSize = 250;

    // Per-key semaphores prevent cache stampede (thundering herd) on cache miss.
    private readonly IMemoryCache _semaphoreCache = new MemoryCache(new MemoryCacheOptions
    {
        SizeLimit = SemaphoreCacheSizeLimit
    });

    private SemaphoreSlim GetSemaphore(string key) =>
        _semaphoreCache.GetOrCreate(key, entry =>
        {
            entry.SlidingExpiration = SemaphoreIdleEvictionWindow;
            entry.Size = 1;
            return new SemaphoreSlim(1, 1);
        })!;

    public void Dispose() => _semaphoreCache.Dispose();

    // ── ICacheService ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or <see langword="null"/>
    /// on a cache miss.
    ///
    /// Zero-allocation hot path: deserialises directly from the <c>byte[]</c> returned
    /// by Redis — no intermediate <c>string</c> allocation.
    /// </summary>
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            // IDistributedCache.GetAsync returns byte[]? — the raw Redis wire bytes.
            // Passing as ReadOnlySpan<byte> to the JSON deserialiser avoids:
            //   • UTF-8 decode → string allocation (old GetStringAsync path)
            //   • string → UTF-8 re-encode inside the JSON parser
            var bytes = await cache.GetAsync(key, ct);
            return bytes is null
                ? null
                : JsonSerializer.Deserialize<T>(bytes.AsSpan(), JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache GET failed for key: {Key}", key);
            return null;
        }
    }

    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="key"/>.
    ///
    /// Zero-allocation hot path: serialises via <see cref="ArrayBufferWriter{T}"/> +
    /// <see cref="Utf8JsonWriter"/> directly to a byte buffer — no intermediate
    /// <c>string</c> or <c>Encoding.UTF8.GetBytes</c> call.
    /// </summary>
    public async Task SetAsync<T>(
        string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
        where T : class
    {
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? DefaultCacheExpiry
            };

            // Serialise directly into a byte buffer — no string intermediate.
            // ArrayBufferWriter<byte> starts with a small internal array and grows
            // as needed (amortised doubling), so most small payloads fit in a single
            // allocation that is then passed to SetAsync.
            var buffer = new ArrayBufferWriter<byte>();
            await using (var writer = new Utf8JsonWriter(buffer))
            {
                JsonSerializer.Serialize(writer, value, JsonOptions);
            }

            // WrittenSpan.ToArray() allocates exactly buffer.WrittenCount bytes —
            // one allocation of the final size (vs. string + byte[] in the old path).
            await cache.SetAsync(key, buffer.WrittenSpan.ToArray(), options, ct);
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
        ct.ThrowIfCancellationRequested();
        try
        {
            var db = multiplexer.GetDatabase();
            foreach (var endpoint in multiplexer.GetEndPoints())
            {
                var server = multiplexer.GetServer(endpoint);
                // SCAN is non-blocking; preferred over KEYS in production.
                var keys = server.Keys(pattern: $"{prefix}*", pageSize: RedisScanPageSize).ToArray();
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
