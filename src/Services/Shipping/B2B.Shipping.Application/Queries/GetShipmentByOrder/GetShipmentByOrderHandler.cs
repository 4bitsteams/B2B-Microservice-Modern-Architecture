using B2B.Shipping.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using MediatR;

namespace B2B.Shipping.Application.Queries.GetShipmentByOrder;

public sealed class GetShipmentByOrderHandler(
    IReadShipmentRepository shipmentRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetShipmentByOrderQuery, Result<ShipmentDto>>
{
    public async Task<Result<ShipmentDto>> Handle(GetShipmentByOrderQuery request, CancellationToken cancellationToken)
    {
        var shipment = await shipmentRepository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (shipment is null || shipment.TenantId != currentUser.TenantId)
            return Error.NotFound("Shipment.NotFound", $"No shipment found for order {request.OrderId}.");

        return new ShipmentDto(shipment.Id, shipment.OrderId, shipment.TrackingNumber,
            shipment.Status.ToString(), shipment.Carrier, shipment.RecipientName,
            shipment.ShippingCost, shipment.EstimatedDelivery,
            shipment.ShippedAt, shipment.DeliveredAt, shipment.CreatedAt);
    }
}
