namespace B2B.Shared.Core.Common;

/// <summary>
/// Thrown by infrastructure when a unique-constraint violation is detected,
/// so Application handlers can return Error.Conflict without referencing EF Core / Npgsql.
/// </summary>
public sealed class UniqueConstraintException(string message) : Exception(message);
