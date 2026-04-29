using B2B.Payment.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Payment.Application.Commands.MarkInvoicePaid;

public sealed class MarkInvoicePaidHandler(
    IInvoiceRepository invoiceRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<MarkInvoicePaidCommand>
{
    public async Task<Result> Handle(MarkInvoicePaidCommand request, CancellationToken cancellationToken)
    {
        var invoice = await invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is null || invoice.TenantId != currentUser.TenantId)
            return Error.NotFound("Invoice.NotFound", $"Invoice {request.InvoiceId} not found.");

        try { invoice.MarkPaid(request.PaymentReference); }
        catch (InvalidOperationException ex)
        {
            return Error.Validation("Invoice.InvalidStatus", ex.Message);
        }

        invoiceRepository.Update(invoice);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
