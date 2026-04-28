using MassTransit;
using B2B.Notification.Worker.Contracts;
using B2B.Notification.Worker.Services;

namespace B2B.Notification.Worker.Consumers;

public sealed class ProductLowStockConsumer(
    IEmailService emailService,
    ILogger<ProductLowStockConsumer> logger)
    : IConsumer<ProductLowStockIntegration>
{
    public async Task Consume(ConsumeContext<ProductLowStockIntegration> context)
    {
        var evt = context.Message;
        var correlationId = ConsumerCorrelationHelper.Restore(context);

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = correlationId,
                   ["ProductName"]   = evt.ProductName
               }))
        {
            logger.LogWarning("Low stock alert: {ProductName} has {CurrentStock} units remaining",
                evt.ProductName, evt.CurrentStock);

            var body = $"""
                <h2>Low Stock Alert ⚠️</h2>
                <p><strong>Product:</strong> {evt.ProductName}</p>
                <p><strong>Current Stock:</strong> {evt.CurrentStock} units</p>
                <p><strong>Threshold:</strong> {evt.Threshold} units</p>
                <p>Please reorder to avoid stockouts.</p>
                """;

            await emailService.SendAsync(new EmailMessage(
                To: evt.TenantAdminEmail,
                Subject: $"Low Stock Alert: {evt.ProductName}",
                Body: body), context.CancellationToken);
        }
    }
}
