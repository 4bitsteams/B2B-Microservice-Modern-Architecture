using B2B.Payment.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Payment.Application.Queries.GetInvoiceById;

public sealed class GetInvoiceByIdHandler(
    IReadInvoiceRepository readRepository,
    ICurrentUser currentUser)
    : IQueryHandler<GetInvoiceByIdQuery, InvoiceDetailDto>
{
    public async Task<Result<InvoiceDetailDto>> Handle(GetInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var invoice = await readRepository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is null || invoice.TenantId != currentUser.TenantId)
            return Error.NotFound("Invoice.NotFound", $"Invoice {request.InvoiceId} not found.");

        return new InvoiceDetailDto(
            invoice.Id,
            invoice.OrderId,
            invoice.InvoiceNumber,
            invoice.TotalAmount,
            invoice.Currency,
            invoice.Status.ToString(),
            invoice.DueAt,
            invoice.PaidAt,
            invoice.CreatedAt,
            invoice.IsOverdue);
    }
}
