using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using B2B.Shared.Core.Messaging;

namespace B2B.Order.Application.Sagas;

/// <summary>
/// MassTransit state machine that orchestrates the complete Order Fulfillment workflow.
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
///
/// ════════════════════════════════════════════════════════════════════════════
/// TIMEOUT SCHEDULING
/// ════════════════════════════════════════════════════════════════════════════
///
///   Timeouts use MassTransit's Schedule mechanism backed by the RabbitMQ
///   delayed-message exchange (requires the rabbitmq_delayed_message_exchange
///   plugin — see docker-compose.yml).  The Guid? token properties on
///   OrderFulfillmentSagaState allow MassTransit to cancel a scheduled message
///   when the expected reply arrives before the deadline.
///
///   To use Quartz.NET instead (preferred for high reliability):
///     x.AddQuartzConsumers();
///     cfg.UseMessageScheduler(new Uri("queue:quartz"));
///
/// ════════════════════════════════════════════════════════════════════════════
/// CORRELATION
/// ════════════════════════════════════════════════════════════════════════════
///
///   CorrelationId == OrderId for all messages.
///   HTTP X-Correlation-ID is propagated transparently as a MassTransit header
///   by MassTransitEventBus — no explicit field is needed in the saga state.
///
/// ════════════════════════════════════════════════════════════════════════════
/// CONCURRENCY
/// ════════════════════════════════════════════════════════════════════════════
///
///   ISagaVersion + EF Core ConcurrencyMode.Optimistic: MassTransit increments
///   Version on every transition and retries on DbUpdateConcurrencyException,
///   preventing two concurrent messages from corrupting the same saga instance.
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
        // Delays come from OrderFulfillmentSagaOptions — configurable per environment
        // without recompiling.  Token stored in saga state allows MassTransit to
        // cancel a scheduled message when the expected reply arrives before the deadline.

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
                // Request stock reservation from Product service
                .Publish(ctx => new ReserveStockCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.Items))
                // Schedule timeout — fires if Product service never responds
                .Schedule(StockReservationTimeout,
                    ctx => new StockReservationTimedOut(ctx.Saga.OrderId))
                .TransitionTo(AwaitingStockReservation));

        // ════════════════════════════════════════════════════════════════════════
        // AwaitingStockReservation
        // ════════════════════════════════════════════════════════════════════════
        During(AwaitingStockReservation,

            // ── Happy path: stock reserved → request payment ──────────────────
            When(StockReserved)
                .Then(ctx =>
                {
                    ctx.Saga.StockReservedAt = DateTime.UtcNow;
                    logger.LogInformation(
                        "Stock reserved for Order {OrderNumber}. Requesting payment.",
                        ctx.Saga.OrderNumber);
                })
                // Cancel the stock timeout — we got the reply
                .Unschedule(StockReservationTimeout)
                // Notify customer that processing has started
                .Publish(ctx => new OrderProcessingStartedIntegration(
                    ctx.Saga.OrderId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerEmail,
                    DateTime.UtcNow))
                // Send payment command to Payment service
                .Publish(ctx => new ProcessPaymentCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.TotalAmount))
                // Schedule payment timeout
                .Schedule(PaymentTimeout,
                    ctx => new PaymentTimedOut(ctx.Saga.OrderId))
                .TransitionTo(AwaitingPayment),

            // ── Failure: stock unavailable → cancel ───────────────────────────
            When(StockReservationFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = ctx.Message.Reason;
                    logger.LogWarning(
                        "Stock reservation failed for Order {OrderNumber}: {Reason}. " +
                        "Cancelling order — no compensating actions needed (stock not reserved).",
                        ctx.Saga.OrderNumber, ctx.Message.Reason);
                })
                .Unschedule(StockReservationTimeout)
                // Notify customer
                .Publish(ctx => new OrderCancelledDueToStockIntegration(
                    ctx.Saga.OrderId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.FailureReason ?? "Insufficient stock",
                    DateTime.UtcNow))
                .Finalize(),

            // ── Timeout: Product service did not respond ───────────────────────
            When(StockReservationTimeout.Received)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = "Stock reservation timed out — no response from Product service.";
                    logger.LogError(
                        "Stock reservation timeout for Order {OrderNumber} after {Deadline}. " +
                        "Cancelling order.",
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

            // ── Happy path: payment succeeded → request shipment ──────────────
            When(PaymentProcessed)
                .Then(ctx =>
                {
                    ctx.Saga.PaymentId          = ctx.Message.PaymentId;
                    ctx.Saga.PaymentProcessedAt = ctx.Message.ProcessedAt;
                    logger.LogInformation(
                        "Payment {PaymentId} processed for Order {OrderNumber}. " +
                        "Requesting shipment creation.",
                        ctx.Message.PaymentId, ctx.Saga.OrderNumber);
                })
                .Unschedule(PaymentTimeout)
                // Notify customer that payment was received
                .Publish(ctx => new OrderPaymentProcessedIntegration(
                    ctx.Saga.OrderId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.PaymentId!.Value,
                    ctx.Saga.TotalAmount,
                    ctx.Saga.PaymentProcessedAt!.Value))
                // Send shipment creation command to Shipping service
                .Publish(ctx => new CreateShipmentCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.Items))
                // Schedule shipment timeout
                .Schedule(ShipmentTimeout,
                    ctx => new ShipmentTimedOut(ctx.Saga.OrderId))
                .TransitionTo(AwaitingShipment),

            // ── Failure: payment declined → compensate stock → cancel ─────────
            When(PaymentFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = ctx.Message.Reason;
                    logger.LogWarning(
                        "Payment failed for Order {OrderNumber}: {Reason}. " +
                        "Compensating: releasing reserved stock.",
                        ctx.Saga.OrderNumber, ctx.Message.Reason);
                })
                .Unschedule(PaymentTimeout)
                // Compensating action 1: release stock
                .Publish(ctx => new ReleaseStockCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.Items))
                // Notify customer
                .Publish(ctx => new OrderCancelledDueToPaymentIntegration(
                    ctx.Saga.OrderId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.FailureReason ?? "Payment declined",
                    DateTime.UtcNow))
                .Finalize(),

            // ── Timeout: Payment service did not respond ───────────────────────
            When(PaymentTimeout.Received)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = "Payment timed out — no response from Payment service.";
                    logger.LogError(
                        "Payment timeout for Order {OrderNumber} after {Deadline}. " +
                        "Compensating: releasing reserved stock.",
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

            // ── Happy path: shipment created → order fully fulfilled ───────────
            When(ShipmentCreated)
                .Then(ctx =>
                {
                    ctx.Saga.ShipmentId        = ctx.Message.ShipmentId;
                    ctx.Saga.TrackingNumber    = ctx.Message.TrackingNumber;
                    ctx.Saga.EstimatedDelivery = ctx.Message.EstimatedDelivery;
                    ctx.Saga.ShipmentCreatedAt = DateTime.UtcNow;
                    logger.LogInformation(
                        "Shipment {ShipmentId} created for Order {OrderNumber}. " +
                        "Tracking: {TrackingNumber}. Order fulfilled!",
                        ctx.Message.ShipmentId, ctx.Saga.OrderNumber, ctx.Message.TrackingNumber);
                })
                .Unschedule(ShipmentTimeout)
                // Notify customer: order is on the way
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

            // ── Failure: carrier rejected → compensate payment + stock → cancel ─
            When(ShipmentFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = ctx.Message.Reason;
                    logger.LogWarning(
                        "Shipment failed for Order {OrderNumber}: {Reason}. " +
                        "Compensating: refunding payment {PaymentId} and releasing stock.",
                        ctx.Saga.OrderNumber, ctx.Message.Reason, ctx.Saga.PaymentId);
                })
                .Unschedule(ShipmentTimeout)
                // Compensating action 1: refund payment
                .Publish(ctx => new RefundPaymentCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.PaymentId!.Value,
                    ctx.Saga.TotalAmount,
                    ctx.Saga.FailureReason ?? "Shipment failed"))
                // Compensating action 2: release stock
                .Publish(ctx => new ReleaseStockCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.Items))
                // Notify customer
                .Publish(ctx => new OrderCancelledDueToShipmentIntegration(
                    ctx.Saga.OrderId,
                    ctx.Saga.OrderNumber,
                    ctx.Saga.CustomerId,
                    ctx.Saga.TenantId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.FailureReason ?? "Shipment creation failed",
                    DateTime.UtcNow))
                .Finalize(),

            // ── Timeout: Shipping service did not respond ──────────────────────
            When(ShipmentTimeout.Received)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = "Shipment timed out — no response from Shipping service.";
                    logger.LogError(
                        "Shipment timeout for Order {OrderNumber} after {Deadline}. " +
                        "Compensating: refunding payment {PaymentId} and releasing stock.",
                        ctx.Saga.OrderNumber, timeouts.ShipmentDeadline, ctx.Saga.PaymentId);
                })
                // Compensating action 1: refund payment
                .Publish(ctx => new RefundPaymentCommand(
                    ctx.Saga.OrderId,
                    ctx.Saga.TenantId,
                    ctx.Saga.PaymentId!.Value,
                    ctx.Saga.TotalAmount,
                    ctx.Saga.FailureReason!))
                // Compensating action 2: release stock
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
