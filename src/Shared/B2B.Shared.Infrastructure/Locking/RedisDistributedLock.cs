using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Locking;

/// <summary>
/// Redis-backed distributed lock using the SET NX PX (set-if-not-exists with expiry) pattern.
///
/// Each lock acquisition stores a unique token as the value so that only the
/// lock owner can release it — preventing accidental release of another
/// instance's lock after a network partition or clock skew.
///
/// Retry loop with configurable interval prevents thundering-herd on contention:
/// multiple callers back off and retry rather than hammering Redis.
/// </summary>
public sealed class RedisDistributedLock(IConnectionMultiplexer redis, ILogger<RedisDistributedLock> logger)
    : IDistributedLock
{
    // Lua script: only delete the key when the stored value matches our token.
    // Atomic: prevents race between GET and DEL across concurrent callers.
    private const string UnlockScript =
        "if redis.call('get', KEYS[1]) == ARGV[1] then " +
        "    return redis.call('del', KEYS[1]) " +
        "else " +
        "    return 0 " +
        "end";

    public async Task<ILockHandle?> AcquireAsync(
        string resource,
        TimeSpan expiry,
        TimeSpan wait,
        TimeSpan retry,
        CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var token = Guid.NewGuid().ToString("N");
        var key = $"lock:{resource}";
        var deadline = DateTime.UtcNow + wait;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var acquired = await db.StringSetAsync(key, token, expiry, When.NotExists);
            if (acquired)
            {
                logger.LogDebug("Distributed lock acquired: {Resource}", resource);
                return new RedisLockHandle(db, key, token, logger);
            }

            logger.LogDebug("Lock contention on {Resource}. Retrying in {RetryMs}ms", resource, retry.TotalMilliseconds);
            await Task.Delay(retry, ct);
        }

        logger.LogWarning("Failed to acquire distributed lock on {Resource} within {WaitMs}ms", resource, wait.TotalMilliseconds);
        return null;
    }

    private sealed class RedisLockHandle(IDatabase db, string key, string token, ILogger logger)
        : ILockHandle
    {
        public bool IsAcquired => true;

        public async ValueTask DisposeAsync()
        {
            try
            {
                await db.ScriptEvaluateAsync(UnlockScript, [key], [token]);
                logger.LogDebug("Distributed lock released: {Key}", key);
            }
            catch (Exception ex)
            {
                // Log but do not rethrow — lock will expire naturally via TTL.
                logger.LogWarning(ex, "Failed to release distributed lock: {Key}", key);
            }
        }
    }
}
