using B2B.Shared.Core.CQRS;

namespace B2B.Payment.Application.Commands.CreateInvoice;

public sealed record CreateInvoiceCommand(
    Guid OrderId,
    decimal Subtotal,
    decimal TaxAmount,
    string Currency,
    int NetTermsDays = 30,
    string? Notes = null) : ICommand<CreateInvoiceResponse>;

public sealed record CreateInvoiceResponse(
    Guid InvoiceId,
    string InvoiceNumber,
    decimal TotalAmount,
    DateTime DueDate);
