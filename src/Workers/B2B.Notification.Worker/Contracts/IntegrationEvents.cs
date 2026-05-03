namespace B2B.Notification.Worker.Contracts;

// Integration events received from Order, Identity, and Product services via Apache Kafka.
// These are local copies — the worker deliberately does not reference B2B.Shared.Core
// to avoid tight coupling.  Keep these in sync with the shared contracts in
// B2B.Shared.Core.Messaging.IntegrationEvents and topic names in KafkaTopics.

// ── Order lifecycle ───────────────────────────────────────────────────────────

public sealed record OrderConfirmedIntegration(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string CustomerEmail,
    decimal TotalAmount,
    DateTime ConfirmedAt);

public sealed record OrderProcessingStartedIntegration(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string CustomerEmail,
    DateTime StartedAt);

public sealed record OrderPaymentProcessedIntegration(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string CustomerEmail,
    Guid PaymentId,
    decimal Amount,
    DateTime ProcessedAt);

public sealed record OrderFulfilledIntegration(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string CustomerEmail,
    Guid ShipmentId,
    string TrackingNumber,
    DateTime EstimatedDelivery,
    DateTime FulfilledAt);

public sealed record OrderCancelledDueToStockIntegration(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string CustomerEmail,
    string Reason,
    DateTime CancelledAt);

public sealed record OrderCancelledDueToPaymentIntegration(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string CustomerEmail,
    string Reason,
    DateTime CancelledAt);

public sealed record OrderCancelledDueToShipmentIntegration(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string CustomerEmail,
    string Reason,
    DateTime CancelledAt);

// ── Shipment ──────────────────────────────────────────────────────────────────

public sealed record OrderShippedIntegration(
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string CustomerEmail,
    string TrackingNumber,
    DateTime ShippedAt);

// ── Identity ──────────────────────────────────────────────────────────────────

public sealed record UserRegisteredIntegration(
    Guid UserId,
    string Email,
    string FullName,
    Guid TenantId,
    DateTime RegisteredAt);

// ── Product ───────────────────────────────────────────────────────────────────

public sealed record ProductLowStockIntegration(
    Guid ProductId,
    string ProductName,
    int CurrentStock,
    int Threshold,
    Guid TenantId,
    string TenantAdminEmail);
