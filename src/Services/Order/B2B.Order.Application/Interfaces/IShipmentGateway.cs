using B2B.Shared.Core.Messaging;

namespace B2B.Order.Application.Interfaces;

/// <summary>
/// Abstraction over the shipment carrier / 3PL used by <see cref="ShipmentConsumer"/>.
///
/// DIP — the consumer (Infrastructure) depends on this interface (Application).
/// Swap the implementation for testing or to integrate a real carrier without
/// touching consumer logic.
///
/// Implementations
/// ───────────────
///   • <c>StubShipmentGateway</c>  — development / test stub (no external calls)
///   • Future: <c>FedExShipmentGateway</c>, <c>UpsShipmentGateway</c>, etc.
/// </summary>
public interface IShipmentGateway
{
    /// <summary>
    /// Creates a shipment record with a carrier and returns tracking details.
    /// Must be idempotent — use <see cref="CreateShipmentCommand.OrderId"/> as the
    /// carrier idempotency key to prevent duplicate shipments on MassTransit retries.
    /// </summary>
    Task<ShipmentGatewayResult> CreateAsync(CreateShipmentCommand command, CancellationToken ct = default);
}

/// <summary>Result returned by <see cref="IShipmentGateway.CreateAsync"/>.</summary>
public sealed record ShipmentGatewayResult(
    bool Succeeded,
    Guid ShipmentId,
    string TrackingNumber,
    DateTime EstimatedDelivery,
    string? FailureReason = null);
