using B2B.Payment.Domain.Entities;
using B2B.Shared.Core.CQRS;

namespace B2B.Payment.Application.Queries.GetPaymentById;

public sealed record GetPaymentByIdQuery(Guid PaymentId) : IQuery<PaymentDto>;

public sealed record PaymentDto(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string Status,
    string Method,
    string? TransactionReference,
    DateTime? ProcessedAt,
    DateTime CreatedAt);
