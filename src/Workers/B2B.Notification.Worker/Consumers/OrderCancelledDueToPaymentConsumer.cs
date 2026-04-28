using MassTransit;
using B2B.Shared.Core.Messaging;
using B2B.Notification.Worker.Services;

namespace B2B.Notification.Worker.Consumers;

/// <summary>
/// Notifies the customer when the Order Fulfillment Saga cancels their order
/// because payment could not be collected (failure or timeout).
/// Stock has already been released by the saga's compensating transaction.
/// </summary>
public sealed class OrderCancelledDueToPaymentConsumer(
    IEmailService emailService,
    ILogger<OrderCancelledDueToPaymentConsumer> logger)
    : IConsumer<OrderCancelledDueToPaymentIntegration>
{
    public async Task Consume(ConsumeContext<OrderCancelledDueToPaymentIntegration> context)
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
                "Order {OrderNumber} cancelled due to payment failure: {Reason}",
                evt.OrderNumber, evt.Reason);

            var body = $"""
                <h2>Order Cancelled — Payment Unsuccessful</h2>
                <p>Unfortunately we were unable to process payment for your order and it has been cancelled.</p>
                <p><strong>Order Number:</strong> {evt.OrderNumber}</p>
                <p><strong>Reason:</strong> {evt.Reason}</p>
                <p><strong>Cancelled At:</strong> {evt.CancelledAt:f}</p>
                <p>No charge has been made to your account. Please check your payment details and try again,
                   or contact our support team if you believe this is an error.</p>
                """;

            await emailService.SendAsync(new EmailMessage(
                To: evt.CustomerEmail,
                Subject: $"Order Cancelled - {evt.OrderNumber}",
                Body: body), context.CancellationToken);
        }
    }
}
