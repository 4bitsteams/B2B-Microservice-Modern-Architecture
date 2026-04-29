using B2B.Payment.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using MediatR;

namespace B2B.Payment.Application.Queries.GetInvoicesByTenant;

public sealed class GetInvoicesByTenantHandler(
    IReadInvoiceRepository invoiceRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetInvoicesByTenantQuery, Result<PagedList<InvoiceDto>>>
{
    public async Task<Result<PagedList<InvoiceDto>>> Handle(GetInvoicesByTenantQuery request, CancellationToken cancellationToken)
    {
        var paged = await invoiceRepository.GetPagedByTenantAsync(
            currentUser.TenantId, request.Page, request.PageSize, cancellationToken);

        var dtos = paged.Items.Select(i => new InvoiceDto(
            i.Id, i.InvoiceNumber, i.OrderId, i.TotalAmount, i.Currency,
            i.Status.ToString(), i.IssuedAt, i.DueAt, i.PaidAt, i.IsOverdue)).ToList();

        return PagedList<InvoiceDto>.Create(dtos, request.Page, request.PageSize, paged.TotalCount);
    }
}
