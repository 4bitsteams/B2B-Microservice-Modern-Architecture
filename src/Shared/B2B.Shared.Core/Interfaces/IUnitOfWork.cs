namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Abstracts the transactional commit boundary for a single database context.
///
/// Command handlers call <see cref="SaveChangesAsync"/> at the end of their
/// work to persist all staged changes atomically. The EF Core implementation
/// also sets <c>IAuditableEntity.CreatedAt</c> / <c>UpdatedAt</c> timestamps
/// and triggers domain event dispatch via <c>DomainEventBehavior</c>.
///
/// Use <see cref="ExecuteInTransactionAsync"/> or
/// <see cref="ExecuteInTransactionAsync{TResult}"/> when multiple
/// <c>SaveChangesAsync</c> calls must be wrapped in a single database
/// transaction — for example:
///   • Inserting a saga state row and an outbox message in the same commit.
///   • Combining <see cref="ILockableRepository{TEntity,TId}.GetByIdForUpdateAsync"/>
///     with a mutation — the lock must remain held until the transaction commits.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all pending EF Core changes to the database and returns the
    /// number of state entries written.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Wraps <paramref name="action"/> in a database transaction.
    /// If <paramref name="action"/> throws, the transaction is rolled back;
    /// otherwise it commits after calling <c>SaveChangesAsync</c> internally.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);

    /// <summary>
    /// Wraps <paramref name="action"/> in a database transaction and returns its result.
    ///
    /// Required when the transactional work must produce a value — for example,
    /// loading a locked aggregate, mutating it, persisting, and returning the
    /// updated state to the caller.
    ///
    /// <code>
    /// var confirmed = await unitOfWork.ExecuteInTransactionAsync(async ct =>
    /// {
    ///     var order = await orderRepo.GetByIdForUpdateAsync(id, ct)
    ///                 ?? return null; // lock not needed for missing row
    ///     order.Confirm();            // mutation under lock
    ///     return order;               // returned after SaveChanges + Commit
    /// }, cancellationToken);
    /// </code>
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default);
}
