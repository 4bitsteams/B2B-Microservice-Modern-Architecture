using MassTransit;
using B2B.Shared.Core.Messaging;
using B2B.Notification.Worker.Services;

namespace B2B.Notification.Worker.Consumers;

/// <summary>
/// Notifies the customer when the Order Fulfillment Saga cancels their order
/// because shipment could not be created (failure or timeout).
/// Payment has already been refunded and stock released by the saga's
/// compensating transaction chain.
/// </summary>
public sealed class OrderCancelledDueToShipmentConsumer(
    IEmailService emailService,
    ILogger<OrderCancelledDueToShipmentConsumer> logger)
    : IConsumer<OrderCancelledDueToShipmentIntegration>
{
    public async Task Consume(ConsumeContext<OrderCancelledDueToShipmentIntegration> context)
    {
        var evt = context.Message;

        var correlationId = ConsumerCorrelationHelper.Restore(context);

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = correlationId,
                   ["OrderNumber"]   = evt.OrderNumber
               }))
        {
            logger.LogWarning(
                "Order {OrderNumber} cancelled due to shipment failure: {Reason}",
                evt.OrderNumber, evt.Reason);

            var body = $"""
                <h2>Order Cancelled — Shipment Could Not Be Created</h2>
                <p>We're sorry, but we were unable to arrange shipment for your order and it has been cancelled.</p>
                <p><strong>Order Number:</strong> {evt.OrderNumber}</p>
                <p><strong>Reason:</strong> {evt.Reason}</p>
                <p><strong>Cancelled At:</strong> {evt.CancelledAt:f}</p>
                <p>Your payment has been fully refunded. Please allow 3–5 business days for the refund
                   to appear on your statement. We apologise for the inconvenience — please contact our
                   support team if you need further assistance.</p>
                """;

            await emailService.SendAsync(new EmailMessage(
                To: evt.CustomerEmail,
                Subject: $"Order Cancelled & Refunded - {evt.OrderNumber}",
                Body: body), context.CancellationToken);
        }
    }
}
