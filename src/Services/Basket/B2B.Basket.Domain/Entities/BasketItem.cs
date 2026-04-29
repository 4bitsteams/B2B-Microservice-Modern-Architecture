using B2B.Shared.Core.Domain;

namespace B2B.Basket.Domain.Entities;

/// <summary>Child entity within the Basket aggregate.</summary>
public sealed class BasketItem : Entity<Guid>
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public string Sku { get; private set; } = default!;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public string? ImageUrl { get; private set; }
    public decimal TotalPrice => Math.Round(UnitPrice * Quantity, 2);

    private BasketItem() { }

    public static BasketItem Create(Guid productId, string productName, string sku,
        decimal unitPrice, int quantity, string? imageUrl)
    {
        return new BasketItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = productName,
            Sku = sku.ToUpperInvariant(),
            UnitPrice = unitPrice,
            Quantity = quantity,
            ImageUrl = imageUrl
        };
    }

    internal void IncrementQuantity(int amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive.", nameof(amount));
        Quantity += amount;
    }

    internal void SetQuantity(int quantity)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        Quantity = quantity;
    }
}
