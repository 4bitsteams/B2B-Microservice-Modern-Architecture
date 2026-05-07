namespace B2B.Shared.Core.Common;

/// <summary>
/// Thrown by infrastructure when a unique-constraint violation is detected,
/// so Application handlers can return Error.Conflict without referencing EF Core / Npgsql.
/// </summary>
public sealed class UniqueConstraintException : Exception
{
    public UniqueConstraintException() { }
    public UniqueConstraintException(string message) : base(message) { }
    public UniqueConstraintException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown by infrastructure when an optimistic-concurrency conflict is detected
/// (EF Core <c>DbUpdateConcurrencyException</c>), so Application handlers can return
/// <see cref="Error.Conflict"/> without referencing EF Core directly.
///
/// <para>
/// Surfaces when two concurrent requests read the same aggregate row, both modify it,
/// and the second writer's row-version no longer matches what was originally read.
/// Callers should return <c>Error.Conflict</c> and ask the client to retry with fresh data.
/// </para>
/// </summary>
public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException() { }
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception inner) : base(message, inner) { }
}
