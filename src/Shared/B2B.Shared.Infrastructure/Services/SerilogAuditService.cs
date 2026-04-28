using Microsoft.Extensions.Logging;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Services;

/// <summary>
/// Audit service that writes structured audit records to Serilog / Seq.
///
/// In production, swap or extend this with a database-backed implementation
/// that persists records to an <c>AuditLog</c> table, or forward to a SIEM.
/// Both implementations satisfy <see cref="IAuditService"/> — no handler changes.
/// </summary>
public sealed class SerilogAuditService(ILogger<SerilogAuditService> logger) : IAuditService
{
    public Task RecordAsync(AuditRecord record, CancellationToken ct = default)
    {
        if (record.Succeeded)
        {
            logger.LogInformation(
                "[AUDIT] {RequestName} | User: {UserId} | Tenant: {TenantId} | Status: Success | At: {OccurredAt}",
                record.RequestName, record.UserId, record.TenantId, record.OccurredAt);
        }
        else
        {
            logger.LogWarning(
                "[AUDIT] {RequestName} | User: {UserId} | Tenant: {TenantId} | Status: Failed | Error: {Error} | At: {OccurredAt}",
                record.RequestName, record.UserId, record.TenantId, record.ErrorMessage, record.OccurredAt);
        }

        return Task.CompletedTask;
    }
}
