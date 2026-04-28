using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using B2B.Order.Application.Sagas;

namespace B2B.Order.Infrastructure.Persistence.Sagas;

/// <summary>
/// EF Core configuration for <see cref="OrderFulfillmentSagaState"/>.
///
/// Table: order_fulfillment_sagas
///
/// One row per in-flight order.  MassTransit writes it on every state transition
/// and deletes it automatically when the saga finalises (SetCompletedWhenFinalized).
///
/// Concurrency token
/// ─────────────────
///   <see cref="OrderFulfillmentSagaState.Version"/> maps to a PostgreSQL integer.
///   MassTransit increments it atomically and retries on
///   <see cref="DbUpdateConcurrencyException"/> to guarantee exactly-once processing.
///
/// Timeout tokens
/// ──────────────
///   StockTimeoutToken, PaymentTimeoutToken, ShipmentTimeoutToken are nullable Guid
///   columns. MassTransit stores the scheduler token here so it can cancel a pending
///   scheduled message when the expected reply arrives before the deadline.
/// </summary>
public sealed class OrderFulfillmentSagaStateMap
    : IEntityTypeConfiguration<OrderFulfillmentSagaState>
{
    public void Configure(EntityTypeBuilder<OrderFulfillmentSagaState> builder)
    {
        builder.ToTable("order_fulfillment_sagas");

        builder.HasKey(x => x.CorrelationId);

        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id");

        builder.Property(x => x.CurrentState)
            .HasColumnName("current_state")
            .HasMaxLength(64)
            .IsRequired();

        // Optimistic concurrency token (ISagaVersion)
        builder.Property(x => x.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();

        // ── Core order data ──────────────────────────────────────────────────────
        builder.Property(x => x.OrderId)
            .HasColumnName("order_id");

        builder.Property(x => x.OrderNumber)
            .HasColumnName("order_number")
            .HasMaxLength(50);

        builder.Property(x => x.CustomerId)
            .HasColumnName("customer_id");

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(x => x.CustomerEmail)
            .HasColumnName("customer_email")
            .HasMaxLength(256);

        builder.Property(x => x.TotalAmount)
            .HasColumnName("total_amount")
            .HasPrecision(18, 2);

        builder.Property(x => x.InitiatedAt)
            .HasColumnName("initiated_at");

        // ── Stock phase ──────────────────────────────────────────────────────────
        builder.Property(x => x.StockReservedAt)
            .HasColumnName("stock_reserved_at");

        // ── Payment phase ────────────────────────────────────────────────────────
        builder.Property(x => x.PaymentId)
            .HasColumnName("payment_id");

        builder.Property(x => x.PaymentProcessedAt)
            .HasColumnName("payment_processed_at");

        // ── Shipment phase ───────────────────────────────────────────────────────
        builder.Property(x => x.ShipmentId)
            .HasColumnName("shipment_id");

        builder.Property(x => x.TrackingNumber)
            .HasColumnName("tracking_number")
            .HasMaxLength(100);

        builder.Property(x => x.EstimatedDelivery)
            .HasColumnName("estimated_delivery");

        builder.Property(x => x.ShipmentCreatedAt)
            .HasColumnName("shipment_created_at");

        // ── Failure ──────────────────────────────────────────────────────────────
        builder.Property(x => x.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(500);

        // ── Timeout schedule tokens ──────────────────────────────────────────────
        // Nullable Guid — null when no timeout is scheduled for that phase.
        builder.Property(x => x.StockTimeoutToken)
            .HasColumnName("stock_timeout_token");

        builder.Property(x => x.PaymentTimeoutToken)
            .HasColumnName("payment_timeout_token");

        builder.Property(x => x.ShipmentTimeoutToken)
            .HasColumnName("shipment_timeout_token");

        // ── Item list (JSONB) ────────────────────────────────────────────────────
        // Stored as PostgreSQL jsonb for efficient storage and future querying.
        builder.Property(x => x.ItemsJson)
            .HasColumnName("items_json")
            .HasColumnType("jsonb")
            .IsRequired();

        // ── Indexes ──────────────────────────────────────────────────────────────
        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("ix_saga_tenant_id");

        builder.HasIndex(x => x.CurrentState)
            .HasDatabaseName("ix_saga_current_state");   // for stuck-saga queries / monitoring
    }
}
