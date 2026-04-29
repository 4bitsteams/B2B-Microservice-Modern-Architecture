using B2B.Shared.Core.Domain;

namespace B2B.Payment.Domain.Events;

public sealed record PaymentCreatedEvent(Guid PaymentId, Guid OrderId, decimal Amount, string Currency) : DomainEvent;
public sealed record PaymentProcessedEvent(Guid PaymentId, Guid OrderId, decimal Amount, string Currency, string TransactionRef) : DomainEvent;
public sealed record PaymentFailedEvent(Guid PaymentId, Guid OrderId, string Reason) : DomainEvent;
public sealed record PaymentRefundedEvent(Guid PaymentId, Guid OrderId, decimal Amount, string Currency) : DomainEvent;
public sealed record InvoiceIssuedEvent(Guid InvoiceId, string InvoiceNumber, decimal TotalAmount, DateTime DueDate) : DomainEvent;
public sealed record InvoicePaidEvent(Guid InvoiceId, string InvoiceNumber, decimal Amount, string PaymentReference) : DomainEvent;
public sealed record InvoiceCancelledEvent(Guid InvoiceId, string InvoiceNumber) : DomainEvent;
