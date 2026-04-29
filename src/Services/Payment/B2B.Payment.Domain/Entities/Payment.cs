using B2B.Payment.Domain.Events;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Payment.Domain.Entities;

public sealed class Payment : AggregateRoot<Guid>, IAuditableEntity
{
    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid TenantId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = default!;
    public PaymentStatus Status { get; private set; }
    public PaymentMethod Method { get; private set; }
    public string? TransactionReference { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private Payment() { }

    public static Payment Create(Guid orderId, Guid customerId, Guid tenantId,
        decimal amount, string currency, PaymentMethod method)
    {
        if (amount <= 0) throw new ArgumentException("Payment amount must be positive.", nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CustomerId = customerId,
            TenantId = tenantId,
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            Method = method,
            Status = PaymentStatus.Pending
        };

        payment.RaiseDomainEvent(new PaymentCreatedEvent(payment.Id, orderId, amount, currency));
        return payment;
    }

    public void Process(string transactionReference)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Cannot process payment in status '{Status}'.");

        ArgumentException.ThrowIfNullOrWhiteSpace(transactionReference);
        Status = PaymentStatus.Completed;
        TransactionReference = transactionReference;
        ProcessedAt = DateTime.UtcNow;
        RaiseDomainEvent(new PaymentProcessedEvent(Id, OrderId, Amount, Currency, TransactionReference));
    }

    public void Fail(string reason)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Cannot fail payment in status '{Status}'.");

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        RaiseDomainEvent(new PaymentFailedEvent(Id, OrderId, reason));
    }

    public void Refund()
    {
        if (Status != PaymentStatus.Completed)
            throw new InvalidOperationException("Only completed payments can be refunded.");

        Status = PaymentStatus.Refunded;
        RaiseDomainEvent(new PaymentRefundedEvent(Id, OrderId, Amount, Currency));
    }
}

public enum PaymentStatus { Pending, Completed, Failed, Refunded, Cancelled }

public enum PaymentMethod { CreditCard, BankTransfer, NetTerms30, NetTerms60, NetTerms90 }
