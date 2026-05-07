namespace B2B.Order.Domain.Entities;

/// <summary>
/// Represents the lifecycle state of an <see cref="Order"/>.
///
/// <para>
/// Valid state-machine transitions:
/// <code>
/// Pending → Confirmed → Processing → Shipped → Delivered
///    ↓           ↓           ↓          ↓
/// Cancelled  Cancelled  Cancelled  Cancelled
/// </code>
/// Mutation methods on <see cref="Order"/> enforce these transitions and return
/// <c>Result</c> failures when an invalid transition is attempted.
/// </para>
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// The order has been submitted but not yet confirmed.
    /// Tax rates, shipping costs, and inventory availability can still be applied.
    /// </summary>
    Pending,

    /// <summary>
    /// The order has been confirmed and locked for fulfillment.
    /// Raises <c>OrderConfirmedEvent</c>, which starts the fulfilment saga.
    /// </summary>
    Confirmed,

    /// <summary>
    /// The order is actively being picked, packed, and prepared for shipment.
    /// Entered via <c>Order.StartProcessing()</c>.
    /// </summary>
    Processing,

    /// <summary>
    /// The order has been handed off to the carrier with a tracking number.
    /// Raises <c>OrderShippedEvent</c>.
    /// </summary>
    Shipped,

    /// <summary>
    /// The carrier has confirmed delivery to the recipient.
    /// Terminal success state — no further transitions are allowed.
    /// Raises <c>OrderDeliveredEvent</c>.
    /// </summary>
    Delivered,

    /// <summary>
    /// The order was cancelled before delivery.
    /// Can be initiated from any state except <see cref="Delivered"/>.
    /// Raises <c>OrderCancelledEvent</c>.
    /// </summary>
    Cancelled
}
