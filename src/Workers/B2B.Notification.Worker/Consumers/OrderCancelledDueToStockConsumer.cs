using MassTransit;
using B2B.Shared.Core.Messaging;
using B2B.Notification.Worker.Services;

namespace B2B.Notification.Worker.Consumers;

/// <summary>
/// Notifies the customer when the Order Fulfillment Saga cancels their order
/// because stock could not be reserved (compensating transaction outcome).
/// </summary>
public sealed class OrderCancelledDueToStockConsumer(
    IEmailService emailService,
    ILogger<OrderCancelledDueToStockConsumer> logger)
    : IConsumer<OrderCancelledDueToStockIntegration>
{
    public async Task Consume(ConsumeContext<OrderCancelledDueToStockIntegration> context)
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
                "Order {OrderNumber} cancelled due to stock unavailability: {Reason}",
                evt.OrderNumber, evt.Reason);

            var body = $"""
                <h2>Order Cancelled — Stock Unavailable</h2>
                <p>We're sorry, but your order could not be fulfilled because one or more items
                   are currently out of stock.</p>
                <p><strong>Order Number:</strong> {evt.OrderNumber}</p>
                <p><strong>Reason:</strong> {evt.Reason}</p>
                <p><strong>Cancelled At:</strong> {evt.CancelledAt:f}</p>
                <p>No payment has been taken. Please check our catalogue for alternative products.</p>
                """;

            await emailService.SendAsync(new EmailMessage(
                To: evt.CustomerEmail,
                Subject: $"Order Cancelled - {evt.OrderNumber}",
                Body: body), context.CancellationToken);
        }
    }
}
