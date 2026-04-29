using B2B.Shared.Core.Domain;

namespace B2B.Order.Domain.Entities;

/// <summary>
/// Child entity within the Order aggregate. Inherits identity-based equality
/// from <see cref="Entity{TId}"/> so two instances with the same Id compare
/// as equal and produce consistent hash codes — consistent with all other
/// domain entities in the bounded context.
/// </summary>
public sealed class OrderItem : Entity<Guid>
{
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public string Sku { get; private set; } = default!;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal TotalPrice => Math.Round(UnitPrice * Quantity, 2);

    private OrderItem() { }

    public static OrderItem Create(Guid orderId, Guid productId, string productName, string sku, decimal unitPrice, int quantity)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        if (unitPrice < 0) throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));

        return new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            ProductName = productName,
            Sku = sku,
            UnitPrice = unitPrice,
            Quantity = quantity
        };
    }

    public void IncrementQuantity(int amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive.", nameof(amount));
        Quantity += amount;
    }
}
