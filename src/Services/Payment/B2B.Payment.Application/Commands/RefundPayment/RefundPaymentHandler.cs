using B2B.Payment.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Payment.Application.Commands.RefundPayment;

public sealed class RefundPaymentHandler(
    IPaymentRepository paymentRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RefundPaymentCommand>
{
    public async Task<Result> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken);
        if (payment is null || payment.TenantId != currentUser.TenantId)
            return Error.NotFound("Payment.NotFound", $"Payment {request.PaymentId} not found.");

        try { payment.Refund(); }
        catch (InvalidOperationException ex)
        {
            return Error.Validation("Payment.InvalidStatus", ex.Message);
        }

        paymentRepository.Update(payment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
