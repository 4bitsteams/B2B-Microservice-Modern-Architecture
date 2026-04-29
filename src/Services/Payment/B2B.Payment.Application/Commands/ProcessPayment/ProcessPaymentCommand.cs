using B2B.Payment.Domain.Entities;
using B2B.Shared.Core.CQRS;

namespace B2B.Payment.Application.Commands.ProcessPayment;

public sealed record ProcessPaymentCommand(
    Guid OrderId,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    string? CardToken = null) : ICommand<ProcessPaymentResponse>;

public sealed record ProcessPaymentResponse(
    Guid PaymentId,
    string Status,
    string? TransactionReference);
