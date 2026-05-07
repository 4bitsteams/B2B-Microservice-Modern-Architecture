using MassTransit;
using Microsoft.Extensions.Logging;
using B2B.Order.Application.Interfaces;
using B2B.Order.Domain.ValueObjects;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Core.Messaging;
using OrderEntity = B2B.Order.Domain.Entities.Order;

namespace B2B.Order.Infrastructure.Messaging;

/// <summary>
/// Creates and confirms an Order when the customer checks out their basket.
/// This bridges the Basket bounded context → Order bounded context without HTTP coupling.
///
/// <para>
/// Flow:
///   BasketCheckedOutIntegration (published by Basket service)
///   → BasketCheckedOutConsumer (this class)
///   → OrderEntity.Create + order.Confirm()
///   → SaveChangesAsync (persists the order)
///   → OrderConfirmedIntegration published directly → OrderFulfillmentSaga starts
/// </para>
///
/// <para>
/// Idempotency: MassTransit guarantees at-least-once delivery. We use the transport
/// MessageId (stable across redeliveries) as a deduplication key stored in Redis.
/// A redelivered message is detected and silently skipped within the TTL window.
/// </para>
///
/// <para>
/// Note: We bypass MediatR here because ICurrentUser is not available in a message consumer.
/// Domain events from order.Confirm() are published manually below rather than via
/// DomainEventBehavior (which is a MediatR pipeline behavior).
/// </para>
/// </summary>
public sealed class BasketCheckedOutConsumer(
    IOrderRepository orderRepository,
    IOrderNumberGenerator orderNumberGenerator,
    ITaxService taxService,
    IUnitOfWork unitOfWork,
    IEventBus eventBus,
    ICacheService cacheService,
    ILogger<BasketCheckedOutConsumer> logger)
    : IConsumer<BasketCheckedOutIntegration>
{
    // Keep the deduplication record long enough to cover any realistic redelivery window.
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromDays(7);

    public async Task Consume(ConsumeContext<BasketCheckedOutIntegration> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

        // ── Idempotency check ─────────────────────────────────────────────────────
        // MassTransit's MessageId is stable across redeliveries of the same message,
        // so it is a safe deduplication key. We store a processed marker in Redis;
        // a second delivery within the TTL window is a no-op.
        var messageId       = context.MessageId?.ToString() ?? Guid.NewGuid().ToString();
        var idempotencyKey  = $"basket-checkout:processed:{messageId}";
        var alreadyHandled  = await cacheService.GetAsync<string>(idempotencyKey, ct);

        if (alreadyHandled is not null)
        {
            logger.LogInformation(
                "Skipping duplicate BasketCheckedOut message {MessageId} — already processed.",
                messageId);
            return;
        }

        logger.LogInformation(
            "Creating order from basket checkout for Customer {CustomerId} (Tenant {TenantId}), " +
            "{ItemCount} item(s), total {TotalAmount:F2}",
            msg.CustomerId, msg.TenantId, msg.Items.Count, msg.TotalAmount);

        var address = Address.Create(msg.Street, msg.City, msg.State, msg.PostalCode, msg.Country);

        var order = OrderEntity.Create(
            msg.CustomerId,
            msg.TenantId,
            address,
            orderNumberGenerator.Generate(),
            msg.Notes);

        foreach (var item in msg.Items)
            order.AddItem(item.ProductId, item.ProductName, item.Sku, item.UnitPrice, item.Quantity);

        // Apply tenant-level tax rate (zero if service unavailable)
        var taxRate = await taxService.GetTaxRateAsync(msg.TenantId, ct: ct);
        order.ApplyTaxRate(taxRate);

        // Confirm immediately — raises OrderConfirmedEvent internally.
        // A freshly created order is always Pending; guard against invariant drift.
        var confirmResult = order.Confirm();
        if (confirmResult.IsFailure)
            throw new InvalidOperationException($"Order confirmation failed: {confirmResult.Error.Description}");

        await orderRepository.AddAsync(order, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Mark message as processed before publishing downstream events.
        // If the publish fails we will retry, but SaveChanges already committed —
        // the idempotency key prevents a second order being created on retry.
        await cacheService.SetAsync(idempotencyKey, "processed", IdempotencyTtl, ct);

        // Publish OrderConfirmedIntegration directly (bypassing MediatR DomainEventBehavior)
        // This starts the OrderFulfillmentSaga.
        var sagaItems = msg.Items
            .Select(i => new OrderItemSagaDetail(i.ProductId, i.Sku, i.Quantity))
            .ToList();

        await eventBus.PublishAsync(new OrderConfirmedIntegration(
            OrderId:       order.Id,
            OrderNumber:   order.OrderNumber,
            CustomerId:    msg.CustomerId,
            TenantId:      msg.TenantId,
            CustomerEmail: msg.CustomerEmail,
            TotalAmount:   order.TotalAmount,
            ConfirmedAt:   DateTime.UtcNow,
            Items:         sagaItems), ct);

        logger.LogInformation(
            "Order {OrderNumber} (Id: {OrderId}) created from basket checkout. Saga started.",
            order.OrderNumber, order.Id);
    }
}
