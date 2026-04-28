namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Compliance audit trail for commands.
///
/// B2B platforms are subject to audit requirements: who changed what order,
/// who approved what price, which admin deactivated which account. This
/// interface captures command-level audit records that can be stored in a
/// dedicated audit table, shipped to an external SIEM, or written to Seq/ELK.
///
/// The <c>AuditBehavior</c> pipeline step calls this automatically for every
/// command that succeeds, so individual handlers stay free of audit concerns.
/// </summary>
public interface IAuditService
{
    Task RecordAsync(AuditRecord record, CancellationToken ct = default);
}
