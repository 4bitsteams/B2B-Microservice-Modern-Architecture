using MassTransit;
using B2B.Notification.Worker.Contracts;
using B2B.Notification.Worker.Services;

namespace B2B.Notification.Worker.Consumers;

public sealed class OrderConfirmedConsumer(
    IEmailService emailService,
    ILogger<OrderConfirmedConsumer> logger)
    : IConsumer<OrderConfirmedIntegration>
{
    public async Task Consume(ConsumeContext<OrderConfirmedIntegration> context)
    {
        var evt = context.Message;

        var correlationId = ConsumerCorrelationHelper.Restore(context);

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = correlationId,
                   ["OrderNumber"]   = evt.OrderNumber
               }))
        {
            logger.LogInformation("Processing OrderConfirmed for Order {OrderNumber}", evt.OrderNumber);

            var body = $"""
                <h2>Order Confirmed</h2>
                <p>Thank you for your order!</p>
                <p><strong>Order Number:</strong> {evt.OrderNumber}</p>
                <p><strong>Total Amount:</strong> ${evt.TotalAmount:F2}</p>
                <p><strong>Confirmed At:</strong> {evt.ConfirmedAt:f}</p>
                <p>We'll send you another email when your order ships.</p>
                """;

            await emailService.SendAsync(new EmailMessage(
                To: evt.CustomerEmail,
                Subject: $"Order Confirmed - {evt.OrderNumber}",
                Body: body), context.CancellationToken);
        }
    }
}
