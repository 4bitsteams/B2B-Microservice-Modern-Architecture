using Microsoft.Extensions.Logging;
using B2B.Order.Application.Interfaces;
using B2B.Shared.Core.Messaging;

namespace B2B.Order.Infrastructure.Messaging;

/// <summary>
/// Development/test stub for <see cref="IShipmentGateway"/>.
///
/// Replace with a real carrier integration (FedEx, UPS, DHL, 3PL) before going
/// to production.  Register the real implementation in DI without changing any
/// consumer code — Open/Closed Principle.
///
/// FAILURE SIMULATION
/// ──────────────────
/// Set environment variable SHIPMENT_STUB_ALWAYS_FAIL=true to force
/// <see cref="CreateAsync"/> to return a failure result, allowing end-to-end
/// testing of the saga's payment-refund + stock-release compensating path.
/// </summary>
public sealed class StubShipmentGateway(ILogger<StubShipmentGateway> logger) : IShipmentGateway
{
    private static readonly bool AlwaysFail =
        string.Equals(
            Environment.GetEnvironmentVariable("SHIPMENT_STUB_ALWAYS_FAIL"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public async Task<ShipmentGatewayResult> CreateAsync(CreateShipmentCommand command, CancellationToken ct = default)
    {
        // Simulate carrier API latency
        await Task.Delay(75, ct);

        if (AlwaysFail)
        {
            logger.LogWarning(
                "StubShipmentGateway: SHIPMENT_STUB_ALWAYS_FAIL=true — returning failure for Order {OrderNumber}",
                command.OrderNumber);
            return new ShipmentGatewayResult(false, Guid.Empty, string.Empty, default, "Stub configured to always fail.");
        }

        var shipmentId     = Guid.NewGuid();
        var trackingNumber = $"TRK-{command.OrderNumber}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var estimatedDelivery = DateTime.UtcNow.AddBusinessDays(5);

        logger.LogInformation(
            "StubShipmentGateway: Shipment {ShipmentId} created for Order {OrderNumber}. Tracking: {TrackingNumber}",
            shipmentId, command.OrderNumber, trackingNumber);

        return new ShipmentGatewayResult(true, shipmentId, trackingNumber, estimatedDelivery);
    }
}

file static class DateTimeExtensions
{
    public static DateTime AddBusinessDays(this DateTime date, int businessDays)
    {
        var result = date;
        var added  = 0;
        while (added < businessDays)
        {
            result = result.AddDays(1);
            if (result.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                added++;
        }
        return result;
    }
}
