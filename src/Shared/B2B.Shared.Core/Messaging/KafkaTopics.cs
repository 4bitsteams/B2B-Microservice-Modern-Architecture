namespace B2B.Shared.Core.Messaging;

/// <summary>
/// Centralised Kafka topic name constants used by all services.
///
/// Publishers and consumers must agree on the same topic name.
/// Services that do not reference <c>B2B.Shared.Core</c> (e.g. the Notification Worker)
/// use the same string values inline — keep both in sync.
/// </summary>
public static class KafkaTopics
{
    // ── Basket ───────────────────────────────────────────────────────────────────

    /// <summary>Basket service → Order service: customer completed checkout.</summary>
    public const string BasketCheckedOut = "b2b-basket-checked-out";

    // ── Order lifecycle ───────────────────────────────────────────────────────────

    /// <summary>Order service → Notification Worker + OrderFulfillmentSaga: order confirmed.</summary>
    public const string OrderConfirmed = "b2b-order-confirmed";

    /// <summary>Fulfillment saga → Notification Worker: order moved to Processing state.</summary>
    public const string OrderProcessingStarted = "b2b-order-processing-started";

    /// <summary>Fulfillment saga → Notification Worker: payment collected successfully.</summary>
    public const string OrderPaymentProcessed = "b2b-order-payment-processed";

    /// <summary>Fulfillment saga → Notification Worker: order fully shipped.</summary>
    public const string OrderFulfilled = "b2b-order-fulfilled";

    /// <summary>Fulfillment saga → Notification Worker: order cancelled — stock unavailable.</summary>
    public const string OrderCancelledStock = "b2b-order-cancelled-stock";

    /// <summary>Fulfillment saga → Notification Worker: order cancelled — payment failed.</summary>
    public const string OrderCancelledPayment = "b2b-order-cancelled-payment";

    /// <summary>Fulfillment saga → Notification Worker: order cancelled — shipment failed.</summary>
    public const string OrderCancelledShipment = "b2b-order-cancelled-shipment";

    // ── Identity ─────────────────────────────────────────────────────────────────

    /// <summary>Identity service → Notification Worker: new user registered.</summary>
    public const string UserRegistered = "b2b-identity-user-registered";

    // ── Product ──────────────────────────────────────────────────────────────────

    /// <summary>Product service → Notification Worker: product stock below threshold.</summary>
    public const string ProductLowStock = "b2b-product-low-stock";
}
