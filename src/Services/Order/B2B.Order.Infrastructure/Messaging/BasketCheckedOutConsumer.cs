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
/// Flow:
///   BasketCheckedOutIntegration (published by Basket service)
///   → BasketCheckedOutConsumer (this class)
///   → OrderEntity.Create + order.Confirm()
///   → SaveChangesAsync (persists the order)
///   → OrderConfirmedIntegration published directly → OrderFulfillmentSaga starts
///
/// Note: We bypass MediatR here because ICurrentUser is not available in a message consumer.
/// Domain events from order.Confirm() are published manually below rather than via
/// DomainEventBehavior (which is a MediatR pipeline behavior).
/// </summary>
public sealed class BasketCheckedOutConsumer(
    IOrderRepository orderRepository,
    IOrderNumberGenerator orderNumberGenerator,
    ITaxService taxService,
    IUnitOfWork unitOfWork,
    IEventBus eventBus,
    ILogger<BasketCheckedOutConsumer> logger)
    : IConsumer<BasketCheckedOutIntegration>
{
    public async Task Consume(ConsumeContext<BasketCheckedOutIntegration> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

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

        // Confirm immediately — raises OrderConfirmedEvent internally
        order.Confirm();

        await orderRepository.AddAsync(order, ct);
        await unitOfWork.SaveChangesAsync(ct);

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
