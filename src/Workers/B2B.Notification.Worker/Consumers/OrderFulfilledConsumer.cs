using MassTransit;
using B2B.Shared.Core.Messaging;
using B2B.Notification.Worker.Services;

namespace B2B.Notification.Worker.Consumers;

/// <summary>
/// Notifies the customer when the Order Fulfillment Saga successfully creates a
/// shipment — the order is fully fulfilled.  Includes the tracking number and
/// estimated delivery date so the customer can follow their parcel.
/// </summary>
public sealed class OrderFulfilledConsumer(
    IEmailService emailService,
    ILogger<OrderFulfilledConsumer> logger)
    : IConsumer<OrderFulfilledIntegration>
{
    public async Task Consume(ConsumeContext<OrderFulfilledIntegration> context)
    {
        var evt = context.Message;

        var correlationId = ConsumerCorrelationHelper.Restore(context);

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"]  = correlationId,
                   ["OrderNumber"]    = evt.OrderNumber,
                   ["ShipmentId"]     = evt.ShipmentId,
                   ["TrackingNumber"] = evt.TrackingNumber
               }))
        {
            logger.LogInformation(
                "Order {OrderNumber} fulfilled — Shipment {ShipmentId}, Tracking: {TrackingNumber}",
                evt.OrderNumber, evt.ShipmentId, evt.TrackingNumber);

            var body = $"""
                <h2>Your Order Has Shipped!</h2>
                <p>Great news — your order is on its way.</p>
                <p><strong>Order Number:</strong> {evt.OrderNumber}</p>
                <p><strong>Shipment Reference:</strong> {evt.ShipmentId}</p>
                <p><strong>Tracking Number:</strong> {evt.TrackingNumber}</p>
                <p><strong>Estimated Delivery:</strong> {evt.EstimatedDelivery:D}</p>
                <p><strong>Shipped At:</strong> {evt.FulfilledAt:f}</p>
                <p>Use your tracking number to follow your parcel with the carrier.</p>
                """;

            await emailService.SendAsync(new EmailMessage(
                To: evt.CustomerEmail,
                Subject: $"Your Order Has Shipped - {evt.OrderNumber}",
                Body: body), context.CancellationToken);
        }
    }
}
