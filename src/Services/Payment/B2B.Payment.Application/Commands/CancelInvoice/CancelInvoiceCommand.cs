using B2B.Shared.Core.CQRS;

namespace B2B.Payment.Application.Commands.CancelInvoice;

public sealed record CancelInvoiceCommand(Guid InvoiceId, string Reason) : ICommand<CancelInvoiceResponse>;

public sealed record CancelInvoiceResponse(Guid InvoiceId, string Status);
