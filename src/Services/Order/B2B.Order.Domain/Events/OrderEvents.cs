using B2B.Shared.Core.Domain;

namespace B2B.Order.Domain.Events;

/// <summary>
/// Raised by <c>Order.Confirm()</c> when an order transitions from
/// <c>Pending</c> to <c>Confirmed</c>.
///
/// <para>
/// Consumed by <c>DomainEventBehavior</c> → <c>OrderFulfillmentSaga</c> via the
/// <c>OrderConfirmedIntegration</c> integration event. Also consumed by the
/// Notification Worker to send an order-confirmation email.
/// </para>
/// </summary>
/// <param name="OrderId">Unique identifier of the confirmed order.</param>
/// <param name="OrderNumber">Human-readable order reference number.</param>
/// <param name="CustomerId">The customer who placed the order.</param>
/// <param name="TenantId">Tenant that owns the order.</param>
/// <param name="TotalAmount">Final total including tax and shipping.</param>
/// <param name="Items">Snapshot of the items ordered, used for inventory reservation.</param>
public sealed record OrderConfirmedEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid TenantId,
    decimal TotalAmount,
    IReadOnlyList<OrderItemDetails> Items) : DomainEvent;

/// <summary>
/// Raised by <c>Order.Ship()</c> when an order transitions from
/// <c>Processing</c> to <c>Shipped</c>.
///
/// Consumed by the Notification Worker to send a shipment-tracking email to the customer.
/// </summary>
/// <param name="OrderId">Unique identifier of the shipped order.</param>
/// <param name="OrderNumber">Human-readable order reference number.</param>
/// <param name="CustomerId">The customer who placed the order.</param>
/// <param name="TrackingNumber">Carrier-assigned shipment tracking number.</param>
public sealed record OrderShippedEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string TrackingNumber) : DomainEvent;

/// <summary>
/// Raised by <c>Order.Deliver()</c> when an order transitions from
/// <c>Shipped</c> to <c>Delivered</c>.
///
/// Consumed by the Notification Worker to send a delivery-confirmation email.
/// </summary>
/// <param name="OrderId">Unique identifier of the delivered order.</param>
/// <param name="OrderNumber">Human-readable order reference number.</param>
/// <param name="CustomerId">The customer who placed the order.</param>
public sealed record OrderDeliveredEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId) : DomainEvent;

/// <summary>
/// Raised by <c>Order.Cancel()</c> when an order is cancelled from any
/// non-terminal status (Pending, Confirmed, Processing, Shipped).
///
/// Consumed by the Notification Worker to send a cancellation email, and by
/// the inventory service to release any reserved stock.
/// </summary>
/// <param name="OrderId">Unique identifier of the cancelled order.</param>
/// <param name="OrderNumber">Human-readable order reference number.</param>
/// <param name="CustomerId">The customer who placed the order.</param>
/// <param name="Reason">Human-readable cancellation reason provided by the requester.</param>
public sealed record OrderCancelledEvent(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string Reason) : DomainEvent;

/// <summary>
/// Lightweight snapshot of a single order line-item, embedded in domain events
/// so consumers do not need to query the Order aggregate for line-item details.
/// </summary>
/// <param name="ProductId">The product identifier.</param>
/// <param name="Sku">Stock-keeping unit code.</param>
/// <param name="Quantity">Number of units.</param>
public sealed record OrderItemDetails(Guid ProductId, string Sku, int Quantity);
