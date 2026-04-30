using System.Text.Json;
using MassTransit;
using B2B.Order.Application.Sagas;
using B2B.Shared.Core.Messaging;

namespace B2B.Order.Infrastructure.Sagas;

/// <summary>
/// Durable state bag for the <see cref="OrderFulfillmentSaga"/>.
///
/// Lifecycle
/// ─────────
///   Created  : when OrderConfirmedIntegration arrives (Initial → AwaitingStockReservation)
///   Updated  : on every state transition (MassTransit increments Version for optimistic locking)
///   Deleted  : when the saga finalises — SetCompletedWhenFinalized removes the row
///
/// DIP
/// ───
///   Placed in Infrastructure (not Application) because it implements
///   <see cref="SagaStateMachineInstance"/> and <see cref="ISagaVersion"/> —
///   both are MassTransit framework types.  Application-layer code that needs
///   to configure the saga references only <see cref="OrderFulfillmentSagaOptions"/>
///   (a plain POCO in the Application layer with no framework coupling).
///
/// Concurrency
/// ───────────
///   <see cref="ISagaVersion"/> instructs MassTransit to use optimistic concurrency.
///   <see cref="Version"/> is incremented atomically on every transition; a
///   <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/> triggers
///   an automatic retry with refreshed state.
///
/// Timeout tokens
/// ──────────────
///   Each AwaitingXxx state schedules a timeout message and stores its token here.
///   The token is used to cancel (unschedule) the message if the expected reply arrives
///   before the deadline — preventing ghost cancellations on already-finalized sagas.
/// </summary>
public sealed class OrderFulfillmentSagaState : SagaStateMachineInstance, ISagaVersion
{
    // ── MassTransit-required ───────────────────────────────────────────────────

    /// <summary>Primary key — always equal to OrderId (CorrelateById strategy).</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Serialised state name: "Initial" | "AwaitingStockReservation" | "AwaitingPayment" | "AwaitingShipment" | "Final".</summary>
    public string CurrentState { get; set; } = null!;

    /// <summary>Optimistic concurrency token — MassTransit increments this on every transition.</summary>
    public int Version { get; set; }

    // ── Core order data ────────────────────────────────────────────────────────

    public Guid    OrderId       { get; set; }
    public string  OrderNumber   { get; set; } = null!;
    public Guid    CustomerId    { get; set; }
    public Guid    TenantId      { get; set; }
    public string  CustomerEmail { get; set; } = string.Empty;
    public decimal TotalAmount   { get; set; }
    public DateTime InitiatedAt  { get; set; }

    // ── Stock phase ────────────────────────────────────────────────────────────

    public DateTime? StockReservedAt { get; set; }

    // ── Payment phase ──────────────────────────────────────────────────────────

    /// <summary>Payment gateway reference — stored for the compensating RefundPaymentCommand.</summary>
    public Guid?     PaymentId          { get; set; }
    public DateTime? PaymentProcessedAt { get; set; }

    // ── Shipment phase ─────────────────────────────────────────────────────────

    public Guid?     ShipmentId        { get; set; }
    public string?   TrackingNumber    { get; set; }
    public DateTime? EstimatedDelivery { get; set; }
    public DateTime? ShipmentCreatedAt { get; set; }

    // ── Failure ────────────────────────────────────────────────────────────────

    public string? FailureReason { get; set; }

    // ── Timeout schedule tokens ────────────────────────────────────────────────
    // MassTransit stores the scheduler token here so it can cancel the scheduled
    // message when the expected reply arrives before the deadline.

    /// <summary>Token for the StockReservationTimedOut scheduled message.</summary>
    public Guid? StockTimeoutToken   { get; set; }

    /// <summary>Token for the PaymentTimedOut scheduled message.</summary>
    public Guid? PaymentTimeoutToken { get; set; }

    /// <summary>Token for the ShipmentTimedOut scheduled message.</summary>
    public Guid? ShipmentTimeoutToken { get; set; }

    // ── Item list (JSONB) ──────────────────────────────────────────────────────
    // EF Core cannot map IReadOnlyList<T> directly; we serialise to a jsonb column.

    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = false };

    public string ItemsJson { get; set; } = "[]";

    /// <summary>Order items needed for stock reservation, release, and shipment commands.</summary>
    public IReadOnlyList<OrderItemSagaDetail> Items
    {
        get => JsonSerializer.Deserialize<List<OrderItemSagaDetail>>(ItemsJson, JsonSerializerOptions) ?? [];
        set => ItemsJson = JsonSerializer.Serialize(value, JsonSerializerOptions);
    }
}
