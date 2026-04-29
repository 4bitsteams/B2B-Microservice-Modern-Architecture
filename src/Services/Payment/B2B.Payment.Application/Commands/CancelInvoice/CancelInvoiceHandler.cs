using B2B.Payment.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Payment.Application.Commands.CancelInvoice;

public sealed class CancelInvoiceHandler(
    IInvoiceRepository invoiceRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CancelInvoiceCommand, CancelInvoiceResponse>
{
    public async Task<Result<CancelInvoiceResponse>> Handle(CancelInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is null || invoice.TenantId != currentUser.TenantId)
            return Error.NotFound("Invoice.NotFound", $"Invoice {request.InvoiceId} not found.");

        try { invoice.Cancel(); }
        catch (InvalidOperationException ex)
        { return Error.Conflict("Invoice.InvalidState", ex.Message); }

        invoiceRepository.Update(invoice);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CancelInvoiceResponse(invoice.Id, invoice.Status.ToString());
    }
}
