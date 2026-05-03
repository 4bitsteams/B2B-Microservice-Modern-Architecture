using B2B.Product.Domain.Events;
using B2B.Product.Domain.ValueObjects;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Product.Domain.Entities;

public sealed class Product : AggregateRoot<Guid>, IAuditableEntity, ITenantEntity
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = string.Empty;
    public string Sku { get; private set; } = default!;
    public Money Price { get; private set; } = default!;
    public Money? CompareAtPrice { get; private set; }
    public int StockQuantity { get; private set; }
    public int LowStockThreshold { get; private set; }
    public Guid CategoryId { get; private set; }
    public Category Category { get; private set; } = default!;
    public Guid TenantId { get; private set; }
    public ProductStatus Status { get; private set; }
    public string? ImageUrl { get; private set; }
    public decimal Weight { get; private set; }
    public string? Tags { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool IsInStock => StockQuantity > 0;
    public bool IsLowStock => StockQuantity <= LowStockThreshold && IsInStock;

    private Product() { }

    public static Product Create(
        string name, string description, string sku,
        Money price, int stockQuantity, Guid categoryId,
        Guid tenantId, int lowStockThreshold = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Sku = sku.ToUpperInvariant(),
            Price = price,
            StockQuantity = stockQuantity,
            LowStockThreshold = lowStockThreshold,
            CategoryId = categoryId,
            TenantId = tenantId,
            Status = ProductStatus.Active
        };

        product.RaiseDomainEvent(new ProductCreatedEvent(product.Id, product.Name, product.TenantId));
        return product;
    }

    public void UpdatePrice(Money newPrice)
    {
        CompareAtPrice = Price;
        Price = newPrice;
        RaiseDomainEvent(new ProductPriceChangedEvent(Id, CompareAtPrice, newPrice));
    }

    public void AdjustStock(int quantity, string reason)
    {
        var previousQuantity = StockQuantity;
        StockQuantity = Math.Max(0, StockQuantity + quantity);
        RaiseDomainEvent(new ProductStockChangedEvent(Id, previousQuantity, StockQuantity, reason));

        if (IsLowStock)
            RaiseDomainEvent(new ProductLowStockEvent(Id, Name, StockQuantity, LowStockThreshold));
    }

    public void DeductStock(int quantity)
    {
        if (StockQuantity < quantity)
            throw new InvalidOperationException($"Insufficient stock. Available: {StockQuantity}, Requested: {quantity}");
        AdjustStock(-quantity, "Sale");
    }

    public void Update(string name, string description, string? imageUrl, string? tags)
    {
        Name = name;
        Description = description;
        ImageUrl = imageUrl;
        Tags = tags;
    }

    public void Activate() => Status = ProductStatus.Active;
    public void Deactivate() => Status = ProductStatus.Inactive;
    public void Archive() => Status = ProductStatus.Archived;
}

public enum ProductStatus { Active, Inactive, Archived }
