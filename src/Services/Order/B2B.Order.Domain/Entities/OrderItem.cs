using B2B.Shared.Core.Domain;

namespace B2B.Order.Domain.Entities;

/// <summary>
/// Child entity within the <see cref="Order"/> aggregate representing a single
/// line-item: one SKU at a given price and quantity.
///
/// <para>
/// Invariants enforced at construction:
/// <list type="bullet">
///   <item><description><see cref="Quantity"/> must be positive.</description></item>
///   <item><description><see cref="UnitPrice"/> cannot be negative (free items are allowed).</description></item>
/// </list>
/// </para>
///
/// <para>
/// Equality is identity-based via <see cref="Entity{TId}"/>: two <see cref="OrderItem"/>
/// instances with the same <c>Id</c> are considered equal regardless of their field values.
/// </para>
/// </summary>
public sealed class OrderItem : Entity<Guid>
{
    /// <summary>Foreign key to the parent <see cref="Order"/>.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>The product being ordered.</summary>
    public Guid ProductId { get; private set; }

    /// <summary>Display name of the product at the time the order was placed (snapshot).</summary>
    public string ProductName { get; private set; } = default!;

    /// <summary>Stock-keeping unit code of the product.</summary>
    public string Sku { get; private set; } = default!;

    /// <summary>Price per unit at the time the order was placed (snapshot).</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Number of units ordered.</summary>
    public int Quantity { get; private set; }

    /// <summary>
    /// Line-item total, rounded to two decimal places.
    /// Computed as <c>UnitPrice × Quantity</c>.
    /// </summary>
    public decimal TotalPrice => Math.Round(UnitPrice * Quantity, 2);

    /// <summary>Parameterless constructor required by EF Core.</summary>
    private OrderItem() { }

    /// <summary>
    /// Creates a validated <see cref="OrderItem"/> and assigns it a new identity.
    /// Called exclusively from <see cref="Order.AddItem"/>.
    /// </summary>
    /// <param name="orderId">Parent order identifier.</param>
    /// <param name="productId">Product being ordered.</param>
    /// <param name="productName">Display name snapshot.</param>
    /// <param name="sku">SKU snapshot.</param>
    /// <param name="unitPrice">Price per unit — must be ≥ 0.</param>
    /// <param name="quantity">Number of units — must be &gt; 0.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="quantity"/> ≤ 0 or <paramref name="unitPrice"/> &lt; 0.
    /// </exception>
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

    /// <summary>
    /// Adds <paramref name="amount"/> units to the existing quantity.
    /// Called from <see cref="Order.AddItem"/> when the same product is added twice.
    /// </summary>
    /// <param name="amount">Number of additional units — must be &gt; 0.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="amount"/> ≤ 0.</exception>
    public void IncrementQuantity(int amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive.", nameof(amount));
        Quantity += amount;
    }
}
