using MediatR;
using B2B.Order.Domain.Events;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Core.Messaging;

namespace B2B.Order.Application.IntegrationEvents;

/// <summary>
/// Bridges the internal <see cref="OrderConfirmedEvent"/> domain event to the
/// cross-service <see cref="OrderConfirmedIntegration"/> integration event.
///
/// Executed by <c>DomainEventBehavior</c> after <c>SaveChangesAsync</c> completes,
/// guaranteeing the order row exists in the database before the message is published
/// to the Kafka topic <c>b2b-order-confirmed</c>.  This is the entry point for the OrderFulfillmentSaga.
///
/// The integration event carries <see cref="OrderItemSagaDetail"/> so the saga can
/// issue stock reservation commands without an additional DB round-trip.
/// </summary>
public sealed class OrderConfirmedIntegrationEventHandler(
    IEventBus eventBus,
    ICurrentUser currentUser)
    : INotificationHandler<OrderConfirmedEvent>
{
    public async Task Handle(OrderConfirmedEvent notification, CancellationToken ct)
    {
        var items = notification.Items
            .Select(i => new OrderItemSagaDetail(i.ProductId, i.Sku, i.Quantity))
            .ToList();

        var integrationEvent = new OrderConfirmedIntegration(
            OrderId:       notification.OrderId,
            OrderNumber:   notification.OrderNumber,
            CustomerId:    notification.CustomerId,
            TenantId:      notification.TenantId,
            CustomerEmail: currentUser.IsAuthenticated ? currentUser.Email ?? string.Empty : string.Empty,
            TotalAmount:   notification.TotalAmount,
            ConfirmedAt:   DateTime.UtcNow,
            Items:         items);

        await eventBus.PublishAsync(integrationEvent, ct);
    }
}
