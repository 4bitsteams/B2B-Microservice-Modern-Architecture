using B2B.Shared.Core.CQRS;

namespace B2B.Payment.Application.Commands.MarkInvoicePaid;

public sealed record MarkInvoicePaidCommand(Guid InvoiceId, string PaymentReference) : ICommand;
