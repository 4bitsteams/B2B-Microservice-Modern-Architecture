using MassTransit;
using B2B.Shared.Core.Messaging;
using B2B.Notification.Worker.Services;

namespace B2B.Notification.Worker.Consumers;

/// <summary>
/// Notifies the customer when the Order Fulfillment Saga successfully processes
/// their payment and moves the order to the shipment stage.
/// </summary>
public sealed class OrderPaymentProcessedConsumer(
    IEmailService emailService,
    ILogger<OrderPaymentProcessedConsumer> logger)
    : IConsumer<OrderPaymentProcessedIntegration>
{
    public async Task Consume(ConsumeContext<OrderPaymentProcessedIntegration> context)
    {
        var evt = context.Message;

        var correlationId = ConsumerCorrelationHelper.Restore(context);

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = correlationId,
                   ["OrderNumber"]   = evt.OrderNumber,
                   ["PaymentId"]     = evt.PaymentId
               }))
        {
            logger.LogInformation(
                "Payment {PaymentId} processed for Order {OrderNumber} — Amount: {Amount:F2}",
                evt.PaymentId, evt.OrderNumber, evt.Amount);

            var body = $"""
                <h2>Payment Confirmed</h2>
                <p>Your payment has been successfully processed. We're now preparing your order for shipment.</p>
                <p><strong>Order Number:</strong> {evt.OrderNumber}</p>
                <p><strong>Payment Reference:</strong> {evt.PaymentId}</p>
                <p><strong>Amount Charged:</strong> {evt.Amount:F2}</p>
                <p><strong>Processed At:</strong> {evt.ProcessedAt:f}</p>
                <p>You'll receive another notification once your order ships.</p>
                """;

            await emailService.SendAsync(new EmailMessage(
                To: evt.CustomerEmail,
                Subject: $"Payment Confirmed - {evt.OrderNumber}",
                Body: body), context.CancellationToken);
        }
    }
}
