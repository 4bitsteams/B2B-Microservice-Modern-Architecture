using B2B.Basket.Domain.Events;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Basket.Domain.Entities;

/// <summary>
/// Basket aggregate root. Persisted exclusively in Redis (not in a relational DB),
/// so it does not implement IAuditableEntity. The basket is keyed by CustomerId.
/// </summary>
public sealed class Basket : AggregateRoot<Guid>
{
    public Guid CustomerId { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime LastModified { get; private set; }

    private readonly List<BasketItem> _items = [];
    public IReadOnlyList<BasketItem> Items => _items.AsReadOnly();

    public decimal TotalPrice => _items.Sum(i => i.TotalPrice);
    public int TotalItems => _items.Sum(i => i.Quantity);

    private Basket() { }

    public static Basket CreateFor(Guid customerId, Guid tenantId)
    {
        return new Basket
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            TenantId = tenantId,
            LastModified = DateTime.UtcNow
        };
    }

    public void AddItem(Guid productId, string productName, string sku, decimal unitPrice, int quantity, string? imageUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        if (unitPrice < 0) throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));

        var existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is not null)
        {
            existing.IncrementQuantity(quantity);
        }
        else
        {
            _items.Add(BasketItem.Create(productId, productName, sku, unitPrice, quantity, imageUrl));
        }

        LastModified = DateTime.UtcNow;
        RaiseDomainEvent(new ItemAddedToBasketEvent(CustomerId, productId, quantity));
    }

    public void UpdateItemQuantity(Guid productId, int newQuantity)
    {
        if (newQuantity <= 0) throw new ArgumentException("Quantity must be positive.", nameof(newQuantity));

        var item = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new InvalidOperationException($"Product {productId} not found in basket.");

        item.SetQuantity(newQuantity);
        LastModified = DateTime.UtcNow;
    }

    public void RemoveItem(Guid productId)
    {
        var item = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new InvalidOperationException($"Product {productId} not found in basket.");

        _items.Remove(item);
        LastModified = DateTime.UtcNow;
        RaiseDomainEvent(new ItemRemovedFromBasketEvent(CustomerId, productId));
    }

    public void Clear()
    {
        _items.Clear();
        LastModified = DateTime.UtcNow;
    }

    public void Checkout()
    {
        if (_items.Count == 0)
            throw new InvalidOperationException("Cannot checkout an empty basket.");

        RaiseDomainEvent(new BasketCheckedOutEvent(CustomerId, TenantId, TotalPrice,
            _items.Select(i => new BasketItemSnapshot(i.ProductId, i.ProductName, i.Sku, i.UnitPrice, i.Quantity)).ToList()));
    }
}
