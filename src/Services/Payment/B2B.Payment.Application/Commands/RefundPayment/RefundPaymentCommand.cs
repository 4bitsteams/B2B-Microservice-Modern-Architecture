using B2B.Shared.Core.CQRS;

namespace B2B.Payment.Application.Commands.RefundPayment;

public sealed record RefundPaymentCommand(Guid PaymentId) : ICommand;
