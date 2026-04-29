using B2B.Payment.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Payment.Application.Queries.GetPaymentsByOrder;

public sealed class GetPaymentsByOrderHandler(
    IReadPaymentRepository readRepository,
    ICurrentUser currentUser)
    : IQueryHandler<GetPaymentsByOrderQuery, IReadOnlyList<PaymentSummaryDto>>
{
    public async Task<Result<IReadOnlyList<PaymentSummaryDto>>> Handle(GetPaymentsByOrderQuery request, CancellationToken cancellationToken)
    {
        var payments = await readRepository.FindAsync(
            p => p.OrderId == request.OrderId && p.TenantId == currentUser.TenantId,
            cancellationToken);

        var dtos = payments
            .Select(p => new PaymentSummaryDto(
                p.Id, p.OrderId, p.Amount, p.Currency, p.Status.ToString(),
                p.ProcessedAt, p.CreatedAt))
            .ToList<PaymentSummaryDto>();

        return Result.Success<IReadOnlyList<PaymentSummaryDto>>(dtos);
    }
}
