using Microsoft.Extensions.Logging;
using B2B.Order.Application.Interfaces;
using B2B.Shared.Core.Messaging;

namespace B2B.Order.Infrastructure.Messaging;

/// <summary>
/// Development/test stub for <see cref="IPaymentGateway"/>.
///
/// Replace with a real gateway implementation (Stripe, Adyen, etc.) before going
/// to production.  Register the real implementation in DI without changing any
/// consumer code — this is the Open/Closed Principle in action.
///
/// FAILURE SIMULATION
/// ──────────────────
/// Set environment variable PAYMENT_STUB_ALWAYS_FAIL=true to force
/// <see cref="ProcessAsync"/> to return a failure result, allowing end-to-end
/// testing of the saga's compensating transaction path.
/// </summary>
public sealed class StubPaymentGateway(ILogger<StubPaymentGateway> logger) : IPaymentGateway
{
    private static readonly bool AlwaysFail =
        string.Equals(
            Environment.GetEnvironmentVariable("PAYMENT_STUB_ALWAYS_FAIL"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public async Task<PaymentGatewayResult> ProcessAsync(ProcessPaymentCommand command, CancellationToken ct = default)
    {
        // Simulate gateway API latency
        await Task.Delay(50, ct);

        if (AlwaysFail)
        {
            logger.LogWarning(
                "StubPaymentGateway: PAYMENT_STUB_ALWAYS_FAIL=true — returning failure for Order {OrderNumber}",
                command.OrderNumber);
            return new PaymentGatewayResult(false, Guid.Empty, "Stub configured to always fail.");
        }

        var paymentId = Guid.NewGuid();
        logger.LogInformation(
            "StubPaymentGateway: Payment {PaymentId} processed for Order {OrderNumber} — {Amount:F2} {Currency}",
            paymentId, command.OrderNumber, command.Amount, command.Currency);

        return new PaymentGatewayResult(true, paymentId);
    }

    public async Task RefundAsync(RefundPaymentCommand command, CancellationToken ct = default)
    {
        // Simulate gateway API latency
        await Task.Delay(30, ct);

        logger.LogInformation(
            "StubPaymentGateway: Refund issued for Payment {PaymentId} (Order {OrderId}) — {Amount:F2} | Reason: {Reason}",
            command.PaymentId, command.OrderId, command.Amount, command.Reason);
    }
}
