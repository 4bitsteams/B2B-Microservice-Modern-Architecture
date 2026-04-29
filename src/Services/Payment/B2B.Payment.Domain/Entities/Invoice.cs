using B2B.Payment.Domain.Events;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Payment.Domain.Entities;

public sealed class Invoice : AggregateRoot<Guid>, IAuditableEntity
{
    public string InvoiceNumber { get; private set; } = default!;
    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid TenantId { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = default!;
    public InvoiceStatus Status { get; private set; }
    public DateTime IssuedAt { get; private set; }
    public DateTime DueAt { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private Invoice() { }

    public static Invoice Create(string invoiceNumber, Guid orderId, Guid customerId, Guid tenantId,
        decimal subtotal, decimal taxAmount, string currency, int netTermsDays = 30, string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);
        if (subtotal < 0) throw new ArgumentException("Subtotal cannot be negative.", nameof(subtotal));

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = invoiceNumber,
            OrderId = orderId,
            CustomerId = customerId,
            TenantId = tenantId,
            Subtotal = subtotal,
            TaxAmount = taxAmount,
            TotalAmount = Math.Round(subtotal + taxAmount, 2),
            Currency = currency.ToUpperInvariant(),
            Status = InvoiceStatus.Issued,
            IssuedAt = DateTime.UtcNow,
            DueAt = DateTime.UtcNow.AddDays(netTermsDays),
            Notes = notes
        };

        invoice.RaiseDomainEvent(new InvoiceIssuedEvent(invoice.Id, invoice.InvoiceNumber, invoice.TotalAmount, invoice.DueAt));
        return invoice;
    }

    public void MarkPaid(string paymentReference)
    {
        if (Status == InvoiceStatus.Paid)
            throw new InvalidOperationException("Invoice is already paid.");
        if (Status == InvoiceStatus.Cancelled)
            throw new InvalidOperationException("Cannot pay a cancelled invoice.");

        Status = InvoiceStatus.Paid;
        PaidAt = DateTime.UtcNow;
        RaiseDomainEvent(new InvoicePaidEvent(Id, InvoiceNumber, TotalAmount, paymentReference));
    }

    public void Cancel()
    {
        if (Status == InvoiceStatus.Paid)
            throw new InvalidOperationException("Cannot cancel a paid invoice.");

        Status = InvoiceStatus.Cancelled;
        RaiseDomainEvent(new InvoiceCancelledEvent(Id, InvoiceNumber));
    }

    public bool IsOverdue => Status == InvoiceStatus.Issued && DateTime.UtcNow > DueAt;
}

public enum InvoiceStatus { Issued, Paid, Overdue, Cancelled }
