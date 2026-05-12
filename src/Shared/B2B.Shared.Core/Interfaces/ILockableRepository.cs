using B2B.Shared.Core.Domain;

namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Extends <see cref="IRepository{TEntity,TId}"/> with PostgreSQL row-level
/// pessimistic locking operations.
///
/// WHEN TO USE
/// ───────────
/// Use pessimistic locking when two concurrent transactions could both read the
/// same row and then overwrite each other's changes without either detecting the
/// conflict — the classic "lost update" problem:
///
///   • Inventory reservation (read stock → check → decrement)
///   • Payment deduction     (read balance → check → debit)
///   • Order status machine  (read status → validate → transition)
///   • Voucher redemption    (read uses-remaining → validate → decrement)
///
/// MODES
/// ─────
/// FOR UPDATE  — exclusive lock. Only one writer at a time. Other transactions
///               attempting to lock the same row will wait.
/// FOR SHARE   — shared lock. Multiple readers can hold it simultaneously, but
///               writers are blocked until all readers release. Use when you need
///               a consistent read but do not intend to write.
///
/// REQUIRED: EXPLICIT TRANSACTION
/// ───────────────────────────────
/// Row locks held by a PostgreSQL statement are released at the end of the
/// transaction — NOT at the end of the statement. Both methods MUST be called
/// inside an explicit transaction started via
/// <see cref="IUnitOfWork.ExecuteInTransactionAsync{TResult}"/>; otherwise the
/// lock is released immediately after the SELECT and provides no protection.
///
/// <code>
/// var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
/// {
///     var order = await repository.GetByIdForUpdateAsync(orderId, ct)
///                 ?? throw new NotFoundException(orderId);
///     order.Confirm();
///     return order;
/// }, cancellationToken);
/// </code>
///
/// SOLID
/// ─────
/// I — Interface Segregation: repositories that do not need locking implement
///     <see cref="IRepository{TEntity,TId}"/> only. Callers needing locks
///     declare a dependency on <see cref="ILockableRepository{TEntity,TId}"/>.
/// D — Dependency Inversion: callers depend on this abstraction; the EF Core
///     implementation lives in Infrastructure.
/// </summary>
/// <typeparam name="TEntity">The aggregate root type.</typeparam>
/// <typeparam name="TId">The identity type of the aggregate.</typeparam>
public interface ILockableRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : AggregateRoot<TId>
    where TId : notnull
{
    /// <summary>
    /// Loads <typeparamref name="TEntity"/> by <paramref name="id"/> and acquires
    /// an exclusive row-level lock (<c>SELECT … FOR UPDATE</c>).
    ///
    /// Concurrent transactions attempting to lock or modify the same row will
    /// block until the current transaction commits or rolls back.
    ///
    /// Returns <see langword="null"/> when the row does not exist (no lock is taken).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called outside of an active database transaction.
    /// </exception>
    Task<TEntity?> GetByIdForUpdateAsync(TId id, CancellationToken ct = default);

    /// <summary>
    /// Loads <typeparamref name="TEntity"/> by <paramref name="id"/> and acquires
    /// a shared row-level lock (<c>SELECT … FOR SHARE</c>).
    ///
    /// Multiple readers can hold the lock simultaneously, but writers are blocked
    /// until all shared-lock holders release. Use when you need a guaranteed-stable
    /// read but do not intend to modify the row yourself.
    ///
    /// Returns <see langword="null"/> when the row does not exist (no lock is taken).
    /// </summary>
    Task<TEntity?> GetByIdForShareAsync(TId id, CancellationToken ct = default);
}
