using B2B.Shipping.Domain.Events;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shipping.Domain.Entities;

public sealed class Shipment : AggregateRoot<Guid>, IAuditableEntity
{
    public Guid OrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public string TrackingNumber { get; private set; } = default!;
    public ShipmentStatus Status { get; private set; }
    public string Carrier { get; private set; } = default!;
    public string RecipientName { get; private set; } = default!;
    public string ShippingAddress { get; private set; } = default!;
    public string City { get; private set; } = default!;
    public string Country { get; private set; } = default!;
    public decimal ShippingCost { get; private set; }
    public string? EstimatedDelivery { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private Shipment() { }

    public static Shipment Create(Guid orderId, Guid tenantId, string carrier,
        string recipientName, string address, string city, string country,
        decimal shippingCost, string? estimatedDelivery = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carrier);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientName);
        if (shippingCost < 0) throw new ArgumentException("Shipping cost cannot be negative.", nameof(shippingCost));

        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            TenantId = tenantId,
            TrackingNumber = GenerateTrackingNumber(),
            Status = ShipmentStatus.Pending,
            Carrier = carrier,
            RecipientName = recipientName,
            ShippingAddress = address,
            City = city,
            Country = country,
            ShippingCost = shippingCost,
            EstimatedDelivery = estimatedDelivery
        };

        shipment.RaiseDomainEvent(new ShipmentCreatedEvent(shipment.Id, orderId, shipment.TrackingNumber));
        return shipment;
    }

    public void Ship()
    {
        if (Status != ShipmentStatus.Pending)
            throw new InvalidOperationException($"Cannot ship from status '{Status}'.");
        Status = ShipmentStatus.Shipped;
        ShippedAt = DateTime.UtcNow;
        RaiseDomainEvent(new ShipmentDispatchedEvent(Id, OrderId, TrackingNumber, Carrier));
    }

    public void UpdateTracking(string newTrackingNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newTrackingNumber);
        TrackingNumber = newTrackingNumber;
    }

    public void MarkDelivered()
    {
        if (Status != ShipmentStatus.Shipped)
            throw new InvalidOperationException($"Cannot deliver from status '{Status}'.");
        Status = ShipmentStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        RaiseDomainEvent(new ShipmentDeliveredEvent(Id, OrderId, DeliveredAt.Value));
    }

    public void Cancel()
    {
        if (Status == ShipmentStatus.Delivered)
            throw new InvalidOperationException("Cannot cancel a delivered shipment.");
        Status = ShipmentStatus.Cancelled;
    }

    private static string GenerateTrackingNumber() =>
        $"B2B-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
}

public enum ShipmentStatus { Pending, Shipped, Delivered, Cancelled, Returned }
