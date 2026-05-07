using B2B.Order.Domain.Events;
using B2B.Order.Domain.ValueObjects;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Order.Domain.Entities;

/// <summary>
/// Aggregate root for the Order bounded context.
///
/// <para>
/// An order progresses through the following lifecycle:
/// <code>
/// Pending → Confirmed → Processing → Shipped → Delivered
///    ↓           ↓           ↓           ↓
///  Cancelled  Cancelled  Cancelled  Cancelled
/// </code>
/// <c>Delivered</c> and <c>Cancelled</c> are terminal states — no further
/// transitions are permitted from either.
/// </para>
///
/// <para>
/// All mutations are performed through named domain methods (<see cref="Confirm"/>,
/// <see cref="Ship"/>, <see cref="Deliver"/>, <see cref="Cancel"/>). Each method
/// enforces the required predecessor status and raises the corresponding domain event,
/// which is published by <c>DomainEventBehavior</c> after <c>SaveChangesAsync</c>.
/// </para>
///
/// <para>
/// Multi-tenancy: every order carries a <see cref="TenantId"/> that is always validated
/// against <c>ICurrentUser.TenantId</c> in command handlers before any state change is applied.
/// </para>
/// </summary>
public sealed class Order : AggregateRoot<Guid>, IAuditableEntity, ITenantEntity
{
    /// <summary>Human-readable, unique order identifier (e.g. "ORD-20240501-00042").</summary>
    public string OrderNumber { get; private set; } = default!;

    /// <summary>The customer who placed this order.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Tenant that owns this order. Used for row-level multi-tenant isolation.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Current lifecycle state of the order. See <see cref="OrderStatus"/> for valid transitions.</summary>
    public OrderStatus Status { get; private set; }

    /// <summary>Destination address for physical delivery of goods.</summary>
    public Address ShippingAddress { get; private set; } = default!;

    /// <summary>Optional billing address. When <see langword="null"/>, the shipping address is used for billing.</summary>
    public Address? BillingAddress { get; private set; }

    /// <summary>Free-form notes from the customer or internal staff (e.g. delivery instructions).</summary>
    public string? Notes { get; private set; }

    /// <summary>Carrier tracking number. Populated when the order transitions to <c>Shipped</c>.</summary>
    public string? TrackingNumber { get; private set; }

    /// <summary>UTC timestamp of when the order was handed to the carrier. <see langword="null"/> until shipped.</summary>
    public DateTime? ShippedAt { get; private set; }

    /// <summary>UTC timestamp of confirmed delivery. <see langword="null"/> until delivered.</summary>
    public DateTime? DeliveredAt { get; private set; }

    /// <summary>UTC timestamp of cancellation. <see langword="null"/> unless cancelled.</summary>
    public DateTime? CancelledAt { get; private set; }

    /// <summary>Human-readable reason provided at cancellation time.</summary>
    public string? CancellationReason { get; private set; }

    /// <inheritdoc/>
    public DateTime CreatedAt { get; set; }

    /// <inheritdoc/>
    public DateTime? UpdatedAt { get; set; }

    private readonly List<OrderItem> _items = [];

    /// <summary>Ordered line items. Exposed as a read-only projection; mutate via <see cref="AddItem"/> and <see cref="RemoveItem"/>.</summary>
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    /// <summary>Sum of all line-item totals before tax and shipping.</summary>
    public decimal Subtotal => _items.Sum(i => i.TotalPrice);

    /// <summary>Tax rate (0.0–1.0) applied before confirmation. Set via <see cref="ApplyTaxRate"/>.</summary>
    public decimal TaxRate { get; private set; }
    public decimal TaxAmount => Math.Round(Subtotal * TaxRate, 2);

    /// <summary>Shipping cost. Set via <see cref="ApplyShippingCost"/>.</summary>
    public decimal ShippingCost { get; private set; }
    public decimal TotalAmount => Subtotal + TaxAmount + ShippingCost;
    public int ItemCount => _items.Sum(i => i.Quantity);

    private const string InvalidStatusCode = "Order.InvalidStatus";

    private Order() { }

    /// <summary>
    /// Factory method — the only public way to construct a valid <see cref="Order"/>.
    /// The order is created in <see cref="OrderStatus.Pending"/> status with an empty item list.
    /// </summary>
    /// <param name="customerId">The customer placing the order.</param>
    /// <param name="tenantId">Tenant that owns the order; used for multi-tenant row isolation.</param>
    /// <param name="shippingAddress">Delivery destination. Must be a valid <see cref="Address"/> value object.</param>
    /// <param name="orderNumber">Unique, human-readable order identifier (e.g. "ORD-20240501-00042").</param>
    /// <param name="notes">Optional free-form customer or staff notes.</param>
    /// <param name="billingAddress">Optional billing address; defaults to <paramref name="shippingAddress"/> when <see langword="null"/>.</param>
    /// <returns>A new <see cref="Order"/> in <c>Pending</c> status.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="orderNumber"/> is null or whitespace.</exception>
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

    /// <summary>
    /// Adds a product line item to the order. If the same <paramref name="productId"/> already
    /// exists, its quantity is incremented rather than creating a duplicate entry.
    /// </summary>
    /// <param name="productId">Product being ordered.</param>
    /// <param name="productName">Display name snapshot (denormalized at order time).</param>
    /// <param name="sku">Stock-keeping unit snapshot (denormalized at order time).</param>
    /// <param name="unitPrice">Price per unit at time of order placement.</param>
    /// <param name="quantity">Number of units to add. Must be positive.</param>
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

    /// <summary>
    /// Removes the line item for the given <paramref name="productId"/> from the order.
    /// </summary>
    /// <param name="productId">Product to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown when no item with <paramref name="productId"/> exists.</exception>
    public void RemoveItem(Guid productId)
    {
        var item = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new InvalidOperationException("Item not found in order.");
        _items.Remove(item);
    }

    /// <summary>
    /// Transitions the order from <c>Pending</c> to <c>Confirmed</c> and raises
    /// <see cref="OrderConfirmedEvent"/>, which starts the <c>OrderFulfillmentSaga</c>.
    /// </summary>
    /// <returns>
    /// <see cref="Result.Success()"/> on success; <c>Error.Validation</c> if the order
    /// is not in <c>Pending</c> status.
    /// </returns>
    public Result Confirm()
    {
        if (Status != OrderStatus.Pending)
            return Error.Validation(InvalidStatusCode, $"Order must be in '{OrderStatus.Pending}' status. Current: '{Status}'.");

        Status = OrderStatus.Confirmed;
        RaiseDomainEvent(new OrderConfirmedEvent(Id, OrderNumber, CustomerId, TenantId, TotalAmount,
            [.. _items.Select(i => new OrderItemDetails(i.ProductId, i.Sku, i.Quantity))]));
        return Result.Success();
    }

    /// <summary>
    /// Transitions the order from <c>Confirmed</c> to <c>Processing</c>, indicating
    /// that warehouse fulfillment has begun. No domain event is raised at this stage.
    /// </summary>
    /// <returns>
    /// <see cref="Result.Success()"/> on success; <c>Error.Validation</c> if the order
    /// is not in <c>Confirmed</c> status.
    /// </returns>
    public Result StartProcessing()
    {
        if (Status != OrderStatus.Confirmed)
            return Error.Validation(InvalidStatusCode, $"Order must be in '{OrderStatus.Confirmed}' status. Current: '{Status}'.");

        Status = OrderStatus.Processing;
        return Result.Success();
    }

    /// <summary>
    /// Transitions the order from <c>Processing</c> to <c>Shipped</c>, records
    /// <paramref name="trackingNumber"/> and the shipment timestamp, and raises
    /// <see cref="OrderShippedEvent"/> to trigger a customer notification.
    /// </summary>
    /// <param name="trackingNumber">Carrier-issued tracking identifier.</param>
    /// <returns>
    /// <see cref="Result.Success()"/> on success; <c>Error.Validation</c> if the order
    /// is not in <c>Processing</c> status.
    /// </returns>
    public Result Ship(string trackingNumber)
    {
        if (Status != OrderStatus.Processing)
            return Error.Validation(InvalidStatusCode, $"Order must be in '{OrderStatus.Processing}' status. Current: '{Status}'.");

        Status = OrderStatus.Shipped;
        TrackingNumber = trackingNumber;
        ShippedAt = DateTime.UtcNow;
        RaiseDomainEvent(new OrderShippedEvent(Id, OrderNumber, CustomerId, trackingNumber));
        return Result.Success();
    }

    /// <summary>
    /// Transitions the order from <c>Shipped</c> to <c>Delivered</c> — a terminal state.
    /// Records the delivery timestamp and raises <see cref="OrderDeliveredEvent"/> to
    /// trigger a delivery-confirmation notification to the customer.
    /// </summary>
    /// <returns>
    /// <see cref="Result.Success()"/> on success; <c>Error.Validation</c> if the order
    /// is not in <c>Shipped</c> status.
    /// </returns>
    public Result Deliver()
    {
        if (Status != OrderStatus.Shipped)
            return Error.Validation(InvalidStatusCode, $"Order must be in '{OrderStatus.Shipped}' status. Current: '{Status}'.");

        Status = OrderStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        RaiseDomainEvent(new OrderDeliveredEvent(Id, OrderNumber, CustomerId));
        return Result.Success();
    }

    /// <summary>
    /// Cancels the order from any non-terminal status, records the cancellation
    /// <paramref name="reason"/> and timestamp, and raises <see cref="OrderCancelledEvent"/>
    /// to trigger downstream compensation (e.g. inventory restock, refund initiation).
    /// </summary>
    /// <param name="reason">Human-readable explanation for the cancellation.</param>
    /// <returns>
    /// <see cref="Result.Success()"/> on success; <c>Error.Validation</c> if the order
    /// is already in <c>Delivered</c> or <c>Cancelled</c> status.
    /// </returns>
    public Result Cancel(string reason)
    {
        if (Status is OrderStatus.Delivered or OrderStatus.Cancelled)
            return Error.Validation(InvalidStatusCode, $"Cannot cancel order in status '{Status}'.");

        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = DateTime.UtcNow;
        RaiseDomainEvent(new OrderCancelledEvent(Id, OrderNumber, CustomerId, reason));
        return Result.Success();
    }
}
