using MassTransit;
using B2B.Shared.Core.Messaging;
using B2B.Notification.Worker.Services;

namespace B2B.Notification.Worker.Consumers;

/// <summary>
/// Notifies the customer when the Order Fulfillment Saga transitions the order
/// to Processing state (stock was successfully reserved).
/// </summary>
public sealed class OrderProcessingStartedConsumer(
    IEmailService emailService,
    ILogger<OrderProcessingStartedConsumer> logger)
    : IConsumer<OrderProcessingStartedIntegration>
{
    public async Task Consume(ConsumeContext<OrderProcessingStartedIntegration> context)
    {
        var evt = context.Message;

        var correlationId = ConsumerCorrelationHelper.Restore(context);

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = correlationId,
                   ["OrderNumber"]   = evt.OrderNumber
               }))
        {
            logger.LogInformation("Order {OrderNumber} moved to Processing", evt.OrderNumber);

            var body = $"""
                <h2>Your Order is Being Processed</h2>
                <p>Great news! Stock has been reserved and your order is now being processed.</p>
                <p><strong>Order Number:</strong> {evt.OrderNumber}</p>
                <p><strong>Processing Started:</strong> {evt.StartedAt:f}</p>
                <p>We'll notify you once your order ships.</p>
                """;

            await emailService.SendAsync(new EmailMessage(
                To: evt.CustomerEmail,
                Subject: $"Order Processing - {evt.OrderNumber}",
                Body: body), context.CancellationToken);
        }
    }
}
