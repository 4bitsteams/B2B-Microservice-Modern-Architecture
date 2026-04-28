using MassTransit;
using Microsoft.Extensions.Logging;
using B2B.Order.Application.Interfaces;
using B2B.Shared.Core.Messaging;

namespace B2B.Order.Infrastructure.Messaging;

/// <summary>
/// Processes <see cref="ProcessPaymentCommand"/> messages published by the
/// <see cref="OrderFulfillmentSaga"/> after stock is reserved.
///
/// DIP — delegates to <see cref="IPaymentGateway"/> so the payment provider can be
/// swapped (Stripe → Adyen, stub → real) without touching this consumer.
///
/// In production, replace <see cref="StubPaymentGateway"/> with a real gateway
/// implementation registered in DI.  This file should not need to change.
/// </summary>
public sealed class PaymentConsumer(
    IPaymentGateway gateway,
    ILogger<PaymentConsumer> logger)
    : IConsumer<ProcessPaymentCommand>
{
    public async Task Consume(ConsumeContext<ProcessPaymentCommand> context)
    {
        var cmd = context.Message;
        var ct  = context.CancellationToken;

        logger.LogInformation(
            "Processing payment for Order {OrderNumber} — {Amount:F2} {Currency}",
            cmd.OrderNumber, cmd.Amount, cmd.Currency);

        var result = await gateway.ProcessAsync(cmd, ct);

        if (result.Succeeded)
        {
            await context.Publish(new PaymentProcessedIntegration(
                OrderId:     cmd.OrderId,
                TenantId:    cmd.TenantId,
                PaymentId:   result.PaymentId,
                Amount:      cmd.Amount,
                ProcessedAt: DateTime.UtcNow), ct);
        }
        else
        {
            logger.LogWarning(
                "Payment failed for Order {OrderNumber}: {Reason}",
                cmd.OrderNumber, result.FailureReason);

            await context.Publish(new PaymentFailedIntegration(
                OrderId:  cmd.OrderId,
                TenantId: cmd.TenantId,
                Reason:   result.FailureReason ?? "Payment declined"), ct);
        }
    }
}

/// <summary>
/// Processes <see cref="RefundPaymentCommand"/> — the compensating action published
/// by <see cref="OrderFulfillmentSaga"/> when shipment fails after payment was collected.
///
/// Fire-and-forget from the saga's perspective: no reply event is expected.
/// MassTransit retries on transient failure.
/// </summary>
public sealed class RefundPaymentConsumer(
    IPaymentGateway gateway,
    ILogger<RefundPaymentConsumer> logger)
    : IConsumer<RefundPaymentCommand>
{
    public async Task Consume(ConsumeContext<RefundPaymentCommand> context)
    {
        var cmd = context.Message;

        logger.LogInformation(
            "Refunding Payment {PaymentId} for Order {OrderId} — {Amount:F2} | Reason: {Reason}",
            cmd.PaymentId, cmd.OrderId, cmd.Amount, cmd.Reason);

        await gateway.RefundAsync(cmd, context.CancellationToken);

        logger.LogInformation(
            "Refund for Payment {PaymentId} (Order {OrderId}) completed",
            cmd.PaymentId, cmd.OrderId);
    }
}
