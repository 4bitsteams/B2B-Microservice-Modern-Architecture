namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Immutable audit record produced by <see cref="IAuditService"/> for every
/// command that passes through the pipeline.
/// </summary>
public sealed record AuditRecord(
    Guid Id,
    string RequestName,
    Guid? UserId,
    Guid? TenantId,
    string? UserEmail,
    string RequestPayload,
    bool Succeeded,
    string? ErrorMessage,
    DateTime OccurredAt)
{
    /// <summary>Creates a new <see cref="AuditRecord"/> stamped with <see cref="DateTime.UtcNow"/>.</summary>
    public static AuditRecord Create(
        string requestName,
        Guid? userId,
        Guid? tenantId,
        string? userEmail,
        string requestPayload,
        bool succeeded,
        string? errorMessage = null) =>
        new(
            Guid.NewGuid(),
            requestName,
            userId,
            tenantId,
            userEmail,
            requestPayload,
            succeeded,
            errorMessage,
            DateTime.UtcNow);
}
