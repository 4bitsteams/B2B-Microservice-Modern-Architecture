using B2B.Shared.Core.CQRS;

namespace B2B.Payment.Application.Queries.GetInvoiceById;

public sealed record GetInvoiceByIdQuery(Guid InvoiceId) : IQuery<InvoiceDetailDto>;

public sealed record InvoiceDetailDto(
    Guid Id,
    Guid OrderId,
    string InvoiceNumber,
    decimal Amount,
    string Currency,
    string Status,
    DateTime DueDate,
    DateTime? PaidAt,
    DateTime CreatedAt,
    bool IsOverdue);
