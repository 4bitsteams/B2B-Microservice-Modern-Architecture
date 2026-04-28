using B2B.Order.Domain.Events;
using B2B.Order.Domain.ValueObjects;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Order.Domain.Entities;

public sealed class Order : AggregateRoot<Guid>, IAuditableEntity
{
    public string OrderNumber { get; private set; } = default!;
    public Guid CustomerId { get; private set; }
    public Guid TenantId { get; private set; }
    public OrderStatus Status { get; private set; }
    public Address ShippingAddress { get; private set; } = default!;
    public Address? BillingAddress { get; private set; }
    public string? Notes { get; private set; }
    public string? TrackingNumber { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private readonly List<OrderItem> _items = [];
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    public decimal Subtotal => _items.Sum(i => i.TotalPrice);

    /// <summary>Tax rate (0.0–1.0) applied before confirmation. Set via <see cref="ApplyTaxRate"/>.</summary>
    public decimal TaxRate { get; private set; }
    public decimal TaxAmount => Math.Round(Subtotal * TaxRate, 2);

    /// <summary>Shipping cost. Set via <see cref="ApplyShippingCost"/>.</summary>
    public decimal ShippingCost { get; private set; }
    public decimal TotalAmount => Subtotal + TaxAmount + ShippingCost;
    public int ItemCount => _items.Sum(i => i.Quantity);

    private Order() { }

    public static Order Create(
        Guid customerId,
        Guid tenantId,
        Address shippingAddress,
        string orderNumber,
        string? notes = null,
        Address? billingAddress = null)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new ArgumentException("Order number is required.", nameof(orderNumber));

        return new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            CustomerId = customerId,
            TenantId = tenantId,
            ShippingAddress = shippingAddress,
            BillingAddress = billingAddress,
            Notes = notes,
            Status = OrderStatus.Pending
        };
    }

    /// <summary>Applies the jurisdiction-specific tax rate before confirmation.</summary>
    public void ApplyTaxRate(decimal rate)
    {
        if (rate is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(rate), "Tax rate must be between 0 and 1.");
        TaxRate = rate;
    }

    /// <summary>Applies a shipping cost (may be zero for free-shipping promotions).</summary>
    public void ApplyShippingCost(decimal cost)
    {
        if (cost < 0)
            throw new ArgumentOutOfRangeException(nameof(cost), "Shipping cost cannot be negative.");
        ShippingCost = cost;
    }

    public void AddItem(Guid productId, string productName, string sku, decimal unitPrice, int quantity)
    {
        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existingItem is not null)
        {
            existingItem.IncrementQuantity(quantity);
            return;
        }

        _items.Add(OrderItem.Create(Id, productId, productName, sku, unitPrice, quantity));
    }

    public void RemoveItem(Guid productId)
    {
        var item = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new InvalidOperationException("Item not found in order.");
        _items.Remove(item);
    }

    public void Confirm()
    {
        EnsureStatus(OrderStatus.Pending);
        Status = OrderStatus.Confirmed;
        RaiseDomainEvent(new OrderConfirmedEvent(Id, OrderNumber, CustomerId, TenantId, TotalAmount, _items
            .Select(i => new OrderItemDetails(i.ProductId, i.Sku, i.Quantity))
            .ToList()));
    }

    public void StartProcessing()
    {
        EnsureStatus(OrderStatus.Confirmed);
        Status = OrderStatus.Processing;
    }

    public void Ship(string trackingNumber)
    {
        EnsureStatus(OrderStatus.Processing);
        Status = OrderStatus.Shipped;
        TrackingNumber = trackingNumber;
        ShippedAt = DateTime.UtcNow;
        RaiseDomainEvent(new OrderShippedEvent(Id, OrderNumber, CustomerId, trackingNumber));
    }

    public void Deliver()
    {
        EnsureStatus(OrderStatus.Shipped);
        Status = OrderStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        RaiseDomainEvent(new OrderDeliveredEvent(Id, OrderNumber, CustomerId));
    }

    public void Cancel(string reason)
    {
        if (Status is OrderStatus.Delivered or OrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel order in status '{Status}'.");

        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = DateTime.UtcNow;
        RaiseDomainEvent(new OrderCancelledEvent(Id, OrderNumber, CustomerId, reason));
    }

    private void EnsureStatus(OrderStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Order must be in '{expected}' status. Current: '{Status}'.");
    }
}
}
