using MassTransit;
using Microsoft.Extensions.Logging;
using B2B.Order.Application.Interfaces;
using B2B.Shared.Core.Messaging;

namespace B2B.Order.Infrastructure.Messaging;

/// <summary>
/// Processes <see cref="CreateShipmentCommand"/> messages published by the
/// <see cref="OrderFulfillmentSaga"/> after payment succeeds.
///
/// DIP — delegates to <see cref="IShipmentGateway"/> so the carrier integration
/// (FedEx, UPS, DHL, 3PL) can be swapped without touching this consumer.
///
/// In production, replace <see cref="StubShipmentGateway"/> with a real gateway
/// implementation registered in DI.  This file should not need to change.
/// </summary>
public sealed class ShipmentConsumer(
    IShipmentGateway gateway,
    ILogger<ShipmentConsumer> logger)
    : IConsumer<CreateShipmentCommand>
{
    public async Task Consume(ConsumeContext<CreateShipmentCommand> context)
    {
        var cmd = context.Message;
        var ct  = context.CancellationToken;

        logger.LogInformation(
            "Creating shipment for Order {OrderNumber} ({ItemCount} line item(s))",
            cmd.OrderNumber, cmd.Items.Count);

        var result = await gateway.CreateAsync(cmd, ct);

        if (result.Succeeded)
        {
            await context.Publish(new ShipmentCreatedIntegration(
                OrderId:           cmd.OrderId,
                TenantId:          cmd.TenantId,
                ShipmentId:        result.ShipmentId,
                TrackingNumber:    result.TrackingNumber,
                EstimatedDelivery: result.EstimatedDelivery), ct);
        }
        else
        {
            logger.LogWarning(
                "Shipment creation failed for Order {OrderNumber}: {Reason}",
                cmd.OrderNumber, result.FailureReason);

            await context.Publish(new ShipmentFailedIntegration(
                OrderId:  cmd.OrderId,
                TenantId: cmd.TenantId,
                Reason:   result.FailureReason ?? "Carrier unavailable"), ct);
        }
    }
}
