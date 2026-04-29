using B2B.Shared.Core.CQRS;

namespace B2B.Payment.Application.Queries.GetPaymentsByOrder;

public sealed record GetPaymentsByOrderQuery(Guid OrderId) : IQuery<IReadOnlyList<PaymentSummaryDto>>;

public sealed record PaymentSummaryDto(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string Status,
    DateTime? ProcessedAt,
    DateTime CreatedAt);
