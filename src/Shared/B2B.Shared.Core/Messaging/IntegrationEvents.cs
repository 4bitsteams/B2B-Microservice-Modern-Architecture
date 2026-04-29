namespace B2B.Shared.Core.Messaging;

// ─── Basket Integration Events ───────────────────────────────────────────────

/// <summary>
/// Published by the Basket service when a customer completes checkout.
/// Consumed by the Order service to create and confirm an order automatically.
/// </summary>
public sealed record BasketCheckedOutIntegration(
    Guid CustomerId,
    Guid TenantId,
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country,
    string? Notes,
    string CustomerEmail,
    IReadOnlyList<BasketItemIntegration> Items,
    decimal TotalAmount);

/// <summary>A single line item within a <see cref="BasketCheckedOutIntegration"/> event.</summary>
public sealed record BasketItemIntegration(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity);

// ─── Order Integration Events ────────────────────────────────────────────────

/// <summary>
/// Published by the Order service when an order is confirmed.
/// Consumed by:
///   • OrderFulfillmentSaga — triggers stock reservation workflow
///   • Notification Worker  — sends confirmation email to customer
/// </summary>
public sealed record OrderConfirmedIntegration(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid TenantId,
    string CustomerEmail,
    decimal TotalAmount,
    DateTime ConfirmedAt,
    IReadOnlyList<OrderItemSagaDetail> Items);

/// <summary>Published when the fulfillment saga transitions the order to Processing.</summary>
public sealed record OrderProcessingStartedIntegration(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid TenantId,
    string CustomerEmail,
    DateTime StartedAt);

/// <summary>
/// Published by the fulfillment saga when stock could not be reserved and the
/// order is cancelled.  Notification Worker sends a cancellation email.
/// </summary>
public sealed record OrderCancelledDueToStockIntegration(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid TenantId,
    string CustomerEmail,
    string Reason,
    DateTime CancelledAt);

/// <summary>
/// Published by the fulfillment saga when payment processing failed or timed out.
/// Stock is released before this event is published.
/// </summary>
public sealed record OrderCancelledDueToPaymentIntegration(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid TenantId,
    string CustomerEmail,
    string Reason,
    DateTime CancelledAt);

/// <summary>
/// Published by the fulfillment saga when shipment creation failed or timed out.
/// Payment is refunded and stock is released before this event is published.
/// </summary>
public sealed record OrderCancelledDueToShipmentIntegration(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid TenantId,
    string CustomerEmail,
    string Reason,
    DateTime CancelledAt);

/// <summary>
/// Published by the fulfillment saga when payment is successfully processed.
/// Notification Worker sends a payment-received email to the customer.
/// </summary>
public sealed record OrderPaymentProcessedIntegration(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid TenantId,
    string CustomerEmail,
    Guid PaymentId,
    decimal Amount,
    DateTime ProcessedAt);

/// <summary>
/// Published by the fulfillment saga when a shipment is created and the order
/// is fully fulfilled.  Notification Worker sends a shipping confirmation email.
/// </summary>
public sealed record OrderFulfilledIntegration(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid TenantId,
    string CustomerEmail,
    Guid ShipmentId,
    string TrackingNumber,
    DateTime EstimatedDelivery,
    DateTime FulfilledAt);

// ─── Stock Integration Events (Order saga ↔ Product service) ─────────────────

/// <summary>
/// Sent by OrderFulfillmentSaga to the Product service requesting
/// that stock be reserved for all order items atomically.
/// </summary>
public sealed record ReserveStockCommand(
    Guid OrderId,
    Guid TenantId,
    IReadOnlyList<OrderItemSagaDetail> Items);

/// <summary>
/// Published by the Product service when all items in an order were
/// successfully reserved.  Resumes the OrderFulfillmentSaga.
/// </summary>
public sealed record StockReservedIntegration(
    Guid OrderId,
    Guid TenantId);

/// <summary>
/// Published by the Product service when one or more items could not be reserved.
/// Triggers the compensating branch of OrderFulfillmentSaga (order cancellation).
/// </summary>
public sealed record StockReservationFailedIntegration(
    Guid OrderId,
    Guid TenantId,
    string Reason);

/// <summary>
/// Sent by OrderFulfillmentSaga as a compensating action when payment fails
/// or a downstream step cannot be completed after stock was already reserved.
/// Idempotent — adds stock back regardless of prior state.
/// </summary>
public sealed record ReleaseStockCommand(
    Guid OrderId,
    Guid TenantId,
    IReadOnlyList<OrderItemSagaDetail> Items);

// ─── Payment Integration Events (Order saga ↔ Payment service) ───────────────

/// <summary>
/// Sent by OrderFulfillmentSaga to the Payment service after stock is reserved.
/// The Payment service charges the customer and publishes the result.
/// </summary>
public sealed record ProcessPaymentCommand(
    Guid OrderId,
    Guid TenantId,
    Guid CustomerId,
    string CustomerEmail,
    string OrderNumber,
    decimal Amount,
    string Currency = "USD");

/// <summary>
/// Published by the Payment service when the charge succeeds.
/// Resumes the OrderFulfillmentSaga → AwaitingShipment.
/// </summary>
public sealed record PaymentProcessedIntegration(
    Guid OrderId,
    Guid TenantId,
    Guid PaymentId,
    decimal Amount,
    DateTime ProcessedAt);

/// <summary>
/// Published by the Payment service when the charge fails (declined, insufficient funds, etc.).
/// Triggers the compensating branch: release stock → cancel order.
/// </summary>
public sealed record PaymentFailedIntegration(
    Guid OrderId,
    Guid TenantId,
    string Reason);

/// <summary>
/// Sent by OrderFulfillmentSaga as a compensating action when shipment fails
/// after payment was already collected.  The Payment service issues a full refund.
/// Idempotent — safe to call multiple times (gateway deduplicates by PaymentId).
/// </summary>
public sealed record RefundPaymentCommand(
    Guid OrderId,
    Guid TenantId,
    Guid PaymentId,
    decimal Amount,
    string Reason);

// ─── Shipment Integration Events (Order saga ↔ Shipping service) ─────────────

/// <summary>
/// Sent by OrderFulfillmentSaga to the Shipping service after payment succeeds.
/// The Shipping service creates a shipment record and assigns a carrier.
/// </summary>
public sealed record CreateShipmentCommand(
    Guid OrderId,
    Guid TenantId,
    string OrderNumber,
    string CustomerEmail,
    IReadOnlyList<OrderItemSagaDetail> Items);

/// <summary>
/// Published by the Shipping service when a shipment is created and a tracking
/// number is assigned.  Finalises the OrderFulfillmentSaga (happy path).
/// </summary>
public sealed record ShipmentCreatedIntegration(
    Guid OrderId,
    Guid TenantId,
    Guid ShipmentId,
    string TrackingNumber,
    DateTime EstimatedDelivery);

/// <summary>
/// Published by the Shipping service when shipment creation fails
/// (carrier unavailable, address validation failure, etc.).
/// Triggers compensating path: refund payment → release stock → cancel order.
/// </summary>
public sealed record ShipmentFailedIntegration(
    Guid OrderId,
    Guid TenantId,
    string Reason);

// ─── Saga Timeout Events ──────────────────────────────────────────────────────

/// <summary>
/// Scheduled by the saga on entry to AwaitingStockReservation.
/// Fired if no stock response arrives within the deadline.
/// </summary>
public sealed record StockReservationTimedOut(Guid OrderId);

/// <summary>
/// Scheduled by the saga on entry to AwaitingPayment.
/// Fired if no payment response arrives within the deadline.
/// </summary>
public sealed record PaymentTimedOut(Guid OrderId);

/// <summary>
/// Scheduled by the saga on entry to AwaitingShipment.
/// Fired if no shipment response arrives within the deadline.
/// </summary>
public sealed record ShipmentTimedOut(Guid OrderId);

// ─── Shared value types ───────────────────────────────────────────────────────

/// <summary>Identifies a product and quantity within a saga-level stock operation.</summary>
public sealed record OrderItemSagaDetail(
    Guid ProductId,
    string Sku,
    int Quantity);
