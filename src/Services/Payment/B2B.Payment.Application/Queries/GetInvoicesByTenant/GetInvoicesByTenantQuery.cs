using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;

namespace B2B.Payment.Application.Queries.GetInvoicesByTenant;

public sealed record GetInvoicesByTenantQuery(int Page = 1, int PageSize = 20) : IQuery<PagedList<InvoiceDto>>;

public sealed record InvoiceDto(
    Guid Id,
    string InvoiceNumber,
    Guid OrderId,
    decimal TotalAmount,
    string Currency,
    string Status,
    DateTime IssuedAt,
    DateTime DueAt,
    DateTime? PaidAt,
    bool IsOverdue);
