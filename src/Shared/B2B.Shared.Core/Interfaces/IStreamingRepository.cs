using System.Linq.Expressions;
using B2B.Shared.Core.Domain;

namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Extends <see cref="IRepository{TEntity,TId}"/> with server-side cursor streaming
/// for large result sets.
///
/// PURPOSE
/// ───────
/// <see cref="IRepository{TEntity,TId}.FindAsync"/> buffers the entire result set in memory
/// before returning. For large collections this causes:
///   • GC pressure from one large <c>List&lt;T&gt;</c> allocation.
///   • Working-set spike while the list is populated.
///   • No ability to start processing the first record until the last is fetched.
///
/// <see cref="StreamAsync"/> uses EF Core's <c>AsAsyncEnumerable()</c> to open a server-side
/// cursor so records flow one-at-a-time, bounding memory consumption to O(batch window)
/// rather than O(total result set).
///
/// USAGE
/// ─────
/// <code>
/// // Stream all pending orders for a tenant without loading them all into memory.
/// await foreach (var order in orderRepository.StreamAsync(
///     o => o.TenantId == tenantId &amp;&amp; o.Status == OrderStatus.Pending,
///     cancellationToken))
/// {
///     await ProcessOrderAsync(order, cancellationToken);
/// }
/// </code>
///
/// GUARANTEES
/// ──────────
/// • Cancellation-safe: the <paramref name="ct"/> flows into EF Core's async enumerator
///   via the <c>[EnumeratorCancellation]</c> attribute so each <c>MoveNextAsync</c> call
///   checks the token.
/// • No buffering: rows arrive one-at-a-time from PostgreSQL via Npgsql's streaming cursor.
/// • Change-tracking: entities are tracked (write repository context) so callers can
///   mutate and save within the same unit of work.
///
/// SOLID
/// ─────
/// ISP — separated from <see cref="IRepository{TEntity,TId}"/> so repositories that do not
/// need streaming do not inherit the capability. Only aggregate repositories that expose
/// large collections need to implement this interface.
/// </summary>
/// <typeparam name="TEntity">The aggregate root type.</typeparam>
/// <typeparam name="TId">The aggregate root identifier type.</typeparam>
public interface IStreamingRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : AggregateRoot<TId>
    where TId : notnull
{
    /// <summary>
    /// Streams entities matching <paramref name="predicate"/> one at a time using
    /// a server-side cursor. When <paramref name="predicate"/> is <see langword="null"/>,
    /// all entities in the table are streamed.
    ///
    /// The returned <see cref="IAsyncEnumerable{T}"/> does not begin executing until
    /// the caller starts enumerating with <c>await foreach</c>.
    /// </summary>
    /// <param name="predicate">
    /// Optional filter expression translated to a SQL <c>WHERE</c> clause by EF Core.
    /// </param>
    /// <param name="ct">
    /// Cancellation token. Propagated into each <c>MoveNextAsync</c> call via
    /// <c>[EnumeratorCancellation]</c> so cancellation is detected between rows.
    /// </param>
    IAsyncEnumerable<TEntity> StreamAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default);
}
