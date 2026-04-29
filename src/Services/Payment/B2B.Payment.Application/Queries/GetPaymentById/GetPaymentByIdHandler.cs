using B2B.Payment.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using MediatR;

namespace B2B.Payment.Application.Queries.GetPaymentById;

public sealed class GetPaymentByIdHandler(
    IReadPaymentRepository paymentRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetPaymentByIdQuery, Result<PaymentDto>>
{
    public async Task<Result<PaymentDto>> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken);
        if (payment is null || payment.TenantId != currentUser.TenantId)
            return Error.NotFound("Payment.NotFound", $"Payment {request.PaymentId} not found.");

        return new PaymentDto(payment.Id, payment.OrderId, payment.CustomerId,
            payment.Amount, payment.Currency, payment.Status.ToString(),
            payment.Method.ToString(), payment.TransactionReference, payment.ProcessedAt, payment.CreatedAt);
    }
}
