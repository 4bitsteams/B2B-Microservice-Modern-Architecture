using B2B.Payment.Application.Interfaces;
using B2B.Payment.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Payment.Application.Commands.CreateInvoice;

public sealed class CreateInvoiceHandler(
    IInvoiceRepository invoiceRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateInvoiceCommand, CreateInvoiceResponse>
{
    public async Task<Result<CreateInvoiceResponse>> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var existing = await invoiceRepository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (existing is not null)
            return Error.Conflict("Invoice.AlreadyExists", $"An invoice for order {request.OrderId} already exists.");

        var invoiceNumber = await invoiceRepository.GenerateInvoiceNumberAsync(cancellationToken);
        var invoice = Invoice.Create(
            invoiceNumber, request.OrderId, currentUser.UserId, currentUser.TenantId,
            request.Subtotal, request.TaxAmount, request.Currency, request.NetTermsDays, request.Notes);

        await invoiceRepository.AddAsync(invoice, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateInvoiceResponse(invoice.Id, invoice.InvoiceNumber, invoice.TotalAmount, invoice.DueAt);
    }
}
