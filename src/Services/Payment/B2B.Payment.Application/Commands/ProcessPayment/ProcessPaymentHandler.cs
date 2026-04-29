using B2B.Payment.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;
using PaymentEntity = B2B.Payment.Domain.Entities.Payment;

namespace B2B.Payment.Application.Commands.ProcessPayment;

public sealed class ProcessPaymentHandler(
    IPaymentRepository paymentRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ProcessPaymentCommand, ProcessPaymentResponse>
{
    public async Task<Result<ProcessPaymentResponse>> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        // Idempotency: prevent duplicate payment for same order
        var existing = await paymentRepository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (existing is not null)
            return Error.Conflict("Payment.AlreadyExists", $"A payment for order {request.OrderId} already exists.");

        var payment = PaymentEntity.Create(
            request.OrderId, currentUser.UserId, currentUser.TenantId,
            request.Amount, request.Currency, request.Method);

        // Simulate payment gateway (replace with real gateway client in production)
        var transactionRef = $"TXN-{Guid.NewGuid():N}".ToUpperInvariant();
        payment.Process(transactionRef);

        await paymentRepository.AddAsync(payment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProcessPaymentResponse(payment.Id, payment.Status.ToString(), payment.TransactionReference);
    }
}
