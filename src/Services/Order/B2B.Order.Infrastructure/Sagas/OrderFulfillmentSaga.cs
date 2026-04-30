using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using B2B.Order.Application.Sagas;
using B2B.Shared.Core.Messaging;

namespace B2B.Order.Infrastructure.Sagas;

/// <summary>
/// MassTransit state machine that orchestrates the complete Order Fulfillment workflow.
///
/// DIP
/// ───
///   Placed in Infrastructure (not Application) because it inherits
///   <see cref="MassTransitStateMachine{TInstance}"/> — a concrete framework type.
///   Business rules (timeouts, workflow steps) are driven by
///   <see cref="OrderFulfillmentSagaOptions"/>, a plain POCO defined in the
///   Application layer with no framework coupling.
///
/// ════════════════════════════════════════════════════════════════════════════
/// STATE DIAGRAM
/// ════════════════════════════════════════════════════════════════════════════
///
///   Initial
///     │  OrderConfirmedIntegration
///     ▼
///   AwaitingStockReservation  ◄─── StockReservationTimedOut (5 min)
///     │  StockReservedIntegration
///     ▼
///   AwaitingPayment  ◄─────────── PaymentTimedOut (10 min)
///     │  PaymentProcessedIntegration
///     ▼
///   AwaitingShipment  ◄──────── ShipmentTimedOut (2 h)
///     │  ShipmentCreatedIntegration
///     ▼
///   Final  ✔  (row deleted)
///
/// ════════════════════════════════════════════════════════════════════════════
/// COMPENSATING TRANSACTION CHAINS
/// ════════════════════════════════════════════════════════════════════════════
///
///   AwaitingStockReservation failure/timeout:
///     → publish OrderCancelledDueToStockIntegration
///     → Finalize
///
///   AwaitingPayment failure/timeout:
///     → ReleaseStockCommand  (compensate stock)
///     → publish OrderCancelledDueToPaymentIntegration
///     → Finalize
///
///   AwaitingShipment failure/timeout:
///     → RefundPaymentCommand  (compensate payment)
///     → ReleaseStockCommand   (compensate stock)
///     → publish OrderCancelledDueToShipmentIntegration
///     → Finalize
/// </summary>
public sealed class OrderFulfillmentSaga
    : MassTransitStateMachine<OrderFulfillmentSagaState>
{
    // ── States ─────────────────────────────────────────────────────────────────

    public State AwaitingStockReservation { get; private set; } = null!;
    public State AwaitingPayment          { get; private set; } = null!;
    public State AwaitingShipment         { get; private set; } = null!;

    // ── Domain events ──────────────────────────────────────────────────────────

    public Event<OrderConfirmedIntegration>         OrderConfirmed         { get; private set; } = null!;
    public Event<StockReservedIntegration>          StockReserved          { get; private set; } = null!;
    public Event<StockReservationFailedIntegration> StockReservationFailed { get; private set; } = null!;
    public Event<PaymentProcessedIntegration>       PaymentProcessed       { get; private set; } = null!;
    public Event<PaymentFailedIntegration>          PaymentFailed          { get; private set; } = null!;
    public Event<ShipmentCreatedIntegration>        ShipmentCreated        { get; private set; } = null!;
    public Event<ShipmentFailedIntegration>         ShipmentFailed         { get; private set; } = null!;

    // ── Timeout scheduled events ───────────────────────────────────────────────

    public Schedule<OrderFulfillmentSagaState, StockReservationTimedOut> StockReservationTimeout { get; private set; } = null!;
    public Schedule<OrderFulfillmentSagaState, PaymentTimedOut>          PaymentTimeout          { get; private set; } = null!;
    public Schedule<OrderFulfillmentSagaState, ShipmentTimedOut>         ShipmentTimeout         { get; private set; } = null!;

    public OrderFulfillmentSaga(
        ILogger<OrderFulfillmentSaga> logger,
        IOptions<OrderFulfillmentSagaOptions> options)
    {
        var timeouts = options.Value;

        // ── State storage ────────────────────────────────────────────────────────
        InstanceState(x => x.CurrentState);

        // ── Correlation — all messages carry OrderId ─────────────────────────────
        Event(() => OrderConfirmed,         e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => StockReserved,          e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => StockReservationFailed, e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentProcessed,       e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentFailed,          e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => ShipmentCreated,        e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => ShipmentFailed,         e => e.CorrelateById(m => m.Message.OrderId));

        // ── Timeout schedules ────────────────────────────────────────────────────
        Schedule(() => StockReservationTimeout,
            state => state.StockTimeoutToken,
            s =>
            {
                s.Delay    = timeouts.StockReservationDeadline;
                s.Received = r => r.CorrelateById(m => m.Message.OrderId);
            });

        Schedule(() => PaymentTimeout,
            state => state.PaymentTimeoutToken,
            s =>
            {
                s.Delay    = timeouts.PaymentDeadline;
                s.Received = r => r.CorrelateById(m => m.Message.OrderId);
            });

        Schedule(() => ShipmentTimeout,
            state => state.ShipmentTimeoutToken,
            s =>
            {
                s.Delay    = timeouts.ShipmentDeadline;
                s.Received = r => r.CorrelateById(m => m.Message.OrderId);
            });

        // ════════════════════════════════════════════════════════════════════════
        // INITIAL → AwaitingStockReservation
        // ════════════════════════════════════════════════════════════════════════
        Initially(
            When(OrderConfirmed)
                .Then(ctx =>
                {
                    ctx.Saga.OrderId       = ctx.Message.OrderId;
                    ctx.Saga.OrderNumber   = ctx.Message.OrderNumber;
                    ctx.Saga.CustomerId    = ctx.Message.CustomerId;
                    ctx.Saga.TenantId      = ctx.Message.TenantId;
                    ctx.Saga.CustomerEmail = ctx.Message.CustomerEmail;
                    ctx.Saga.TotalAmount   = ctx.Message.TotalAmount;
                    ctx.Saga.Items         = ctx.Message.Items;
                    ctx.Saga.InitiatedAt   = DateTime.UtcNow;

                    logger.LogInformation(
                        "Saga started for Order {OrderNumber} (saga {CorrelationId}). " +
                        "Requesting stock reservation for {ItemCount} item(s).",
                        ctx.Saga.OrderNumber, ctx.Saga.CorrelationId, ctx.Saga.Items.Count);
                })
                .Publish(ctx => new ReserveStockCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.Items))
                .Schedule(StockReservationTimeout,
                    ctx => new StockReservationTimedOut(ctx.Saga.OrderId))
                .TransitionTo(AwaitingStockReservation));

        // ════════════════════════════════════════════════════════════════════════
        // AwaitingStockReservation
        // ════════════════════════════════════════════════════════════════════════
        During(AwaitingStockReservation,

            When(StockReserved)
                .Then(ctx =>
                {
                    ctx.Saga.StockReservedAt = DateTime.UtcNow;
                    logger.LogInformation(
                        "Stock reserved for Order {OrderNumber}. Requesting payment.",
                        ctx.Saga.OrderNumber);
                })
                .Unschedule(StockReservationTimeout)
                .Publish(ctx => new OrderProcessingStartedIntegration(
                    ctx.Saga.OrderId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerEmail,
                    DateTime.UtcNow))
                .Publish(ctx => new ProcessPaymentCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.TotalAmount))
                .Schedule(PaymentTimeout,
                    ctx => new PaymentTimedOut(ctx.Saga.OrderId))
                .TransitionTo(AwaitingPayment),

            When(StockReservationFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = ctx.Message.Reason;
                    logger.LogWarning(
                        "Stock reservation failed for Order {OrderNumber}: {Reason}. Cancelling order.",
                        ctx.Saga.OrderNumber, ctx.Message.Reason);
                })
                .Unschedule(StockReservationTimeout)
                .Publish(ctx => new OrderCancelledDueToStockIntegration(
                    ctx.Saga.OrderId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.FailureReason ?? "Insufficient stock",
                    DateTime.UtcNow))
                .Finalize(),

            When(StockReservationTimeout.Received)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = "Stock reservation timed out — no response from Product service.";
                    logger.LogError(
                        "Stock reservation timeout for Order {OrderNumber} after {Deadline}. Cancelling order.",
                        ctx.Saga.OrderNumber, timeouts.StockReservationDeadline);
                })
                .Publish(ctx => new OrderCancelledDueToStockIntegration(
                    ctx.Saga.OrderId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.FailureReason!,
                    DateTime.UtcNow))
                .Finalize());

        // ════════════════════════════════════════════════════════════════════════
        // AwaitingPayment
        // ════════════════════════════════════════════════════════════════════════
        During(AwaitingPayment,

            When(PaymentProcessed)
                .Then(ctx =>
                {
                    ctx.Saga.PaymentId          = ctx.Message.PaymentId;
                    ctx.Saga.PaymentProcessedAt = ctx.Message.ProcessedAt;
                    logger.LogInformation(
                        "Payment {PaymentId} processed for Order {OrderNumber}. Requesting shipment.",
                        ctx.Message.PaymentId, ctx.Saga.OrderNumber);
                })
                .Unschedule(PaymentTimeout)
                .Publish(ctx => new OrderPaymentProcessedIntegration(
                    ctx.Saga.OrderId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.PaymentId!.Value,
                    ctx.Saga.TotalAmount,
                    ctx.Saga.PaymentProcessedAt!.Value))
                .Publish(ctx => new CreateShipmentCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.Items))
                .Schedule(ShipmentTimeout,
                    ctx => new ShipmentTimedOut(ctx.Saga.OrderId))
                .TransitionTo(AwaitingShipment),

            When(PaymentFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = ctx.Message.Reason;
                    logger.LogWarning(
                        "Payment failed for Order {OrderNumber}: {Reason}. Compensating: releasing stock.",
                        ctx.Saga.OrderNumber, ctx.Message.Reason);
                })
                .Unschedule(PaymentTimeout)
                .Publish(ctx => new ReleaseStockCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.Items))
                .Publish(ctx => new OrderCancelledDueToPaymentIntegration(
                    ctx.Saga.OrderId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.FailureReason ?? "Payment declined",
                    DateTime.UtcNow))
                .Finalize(),

            When(PaymentTimeout.Received)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = "Payment timed out — no response from Payment service.";
                    logger.LogError(
                        "Payment timeout for Order {OrderNumber} after {Deadline}. Compensating: releasing stock.",
                        ctx.Saga.OrderNumber, timeouts.PaymentDeadline);
                })
                .Publish(ctx => new ReleaseStockCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.Items))
                .Publish(ctx => new OrderCancelledDueToPaymentIntegration(
                    ctx.Saga.OrderId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.FailureReason!,
                    DateTime.UtcNow))
                .Finalize());

        // ════════════════════════════════════════════════════════════════════════
        // AwaitingShipment
        // ════════════════════════════════════════════════════════════════════════
        During(AwaitingShipment,

            When(ShipmentCreated)
                .Then(ctx =>
                {
                    ctx.Saga.ShipmentId        = ctx.Message.ShipmentId;
                    ctx.Saga.TrackingNumber    = ctx.Message.TrackingNumber;
                    ctx.Saga.EstimatedDelivery = ctx.Message.EstimatedDelivery;
                    ctx.Saga.ShipmentCreatedAt = DateTime.UtcNow;
                    logger.LogInformation(
                        "Shipment {ShipmentId} created for Order {OrderNumber}. Tracking: {TrackingNumber}. Order fulfilled!",
                        ctx.Message.ShipmentId, ctx.Saga.OrderNumber, ctx.Message.TrackingNumber);
                })
                .Unschedule(ShipmentTimeout)
                .Publish(ctx => new OrderFulfilledIntegration(
                    ctx.Saga.OrderId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.ShipmentId!.Value,
                    ctx.Saga.TrackingNumber!,
                    ctx.Saga.EstimatedDelivery!.Value,
                    DateTime.UtcNow))
                .Finalize(),

            When(ShipmentFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = ctx.Message.Reason;
                    logger.LogWarning(
                        "Shipment failed for Order {OrderNumber}: {Reason}. Compensating: refunding {PaymentId} and releasing stock.",
                        ctx.Saga.OrderNumber, ctx.Message.Reason, ctx.Saga.PaymentId);
                })
                .Unschedule(ShipmentTimeout)
                .Publish(ctx => new RefundPaymentCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.PaymentId!.Value,
                    ctx.Saga.TotalAmount,
                    ctx.Saga.FailureReason ?? "Shipment failed"))
                .Publish(ctx => new ReleaseStockCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.Items))
                .Publish(ctx => new OrderCancelledDueToShipmentIntegration(
                    ctx.Saga.OrderId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.FailureReason ?? "Shipment creation failed",
                    DateTime.UtcNow))
                .Finalize(),

            When(ShipmentTimeout.Received)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = "Shipment timed out — no response from Shipping service.";
                    logger.LogError(
                        "Shipment timeout for Order {OrderNumber} after {Deadline}. Compensating: refunding {PaymentId} and releasing stock.",
                        ctx.Saga.OrderNumber, timeouts.ShipmentDeadline, ctx.Saga.PaymentId);
                })
                .Publish(ctx => new RefundPaymentCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.PaymentId!.Value,
                    ctx.Saga.TotalAmount,
                    ctx.Saga.FailureReason!))
                .Publish(ctx => new ReleaseStockCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.Items))
                .Publish(ctx => new OrderCancelledDueToShipmentIntegration(
                    ctx.Saga.OrderId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.FailureReason!,
                    DateTime.UtcNow))
                .Finalize());

        // Remove the saga row once it reaches the Final state
        SetCompletedWhenFinalized();
    }
}
