using B2B.Shipping.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shipping.Application.Commands.CancelShipment;

public sealed class CancelShipmentHandler(
    IShipmentRepository shipmentRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CancelShipmentCommand, CancelShipmentResponse>
{
    public async Task<Result<CancelShipmentResponse>> Handle(CancelShipmentCommand request, CancellationToken cancellationToken)
    {
        var shipment = await shipmentRepository.GetByIdAsync(request.ShipmentId, cancellationToken);
        if (shipment is null || shipment.TenantId != currentUser.TenantId)
            return Error.NotFound("Shipment.NotFound", $"Shipment {request.ShipmentId} not found.");

        try { shipment.Cancel(); }
        catch (InvalidOperationException ex)
        { return Error.Conflict("Shipment.InvalidState", ex.Message); }

        shipmentRepository.Update(shipment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CancelShipmentResponse(shipment.Id, shipment.Status.ToString());
    }
}
