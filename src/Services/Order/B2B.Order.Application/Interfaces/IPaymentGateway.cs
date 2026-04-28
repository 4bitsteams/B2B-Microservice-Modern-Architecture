using B2B.Shared.Core.Messaging;

namespace B2B.Order.Application.Interfaces;

/// <summary>
/// Abstraction over the payment gateway used by <see cref="PaymentConsumer"/> and
/// <see cref="RefundPaymentConsumer"/>.
///
/// DIP — the consumers (Infrastructure) depend on this interface (Application).
/// Swap the implementation for testing or to integrate a real gateway without
/// touching consumer logic.
///
/// Implementations
/// ───────────────
///   • <c>StubPaymentGateway</c>   — development / test stub (no external calls)
///   • Future: <c>StripePaymentGateway</c>, <c>AdyenPaymentGateway</c>, etc.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Charges the customer for the given order.
    /// Must be idempotent — use <see cref="ProcessPaymentCommand.OrderId"/> as the
    /// gateway idempotency key to prevent double-charging on MassTransit retries.
    /// </summary>
    Task<PaymentGatewayResult> ProcessAsync(ProcessPaymentCommand command, CancellationToken ct = default);

    /// <summary>
    /// Refunds a previously collected payment.
    /// Idempotent — calling twice for the same <c>PaymentId</c> is a safe no-op.
    /// </summary>
    Task RefundAsync(RefundPaymentCommand command, CancellationToken ct = default);
}

/// <summary>Result returned by <see cref="IPaymentGateway.ProcessAsync"/>.</summary>
public sealed record PaymentGatewayResult(
    bool Succeeded,
    Guid PaymentId,
    string? FailureReason = null);
