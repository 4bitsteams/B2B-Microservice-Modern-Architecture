using B2B.Shipping.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shipping.Application.Commands.UpdateTrackingInfo;

public sealed class UpdateTrackingInfoHandler(
    IShipmentRepository shipmentRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateTrackingInfoCommand, UpdateTrackingInfoResponse>
{
    public async Task<Result<UpdateTrackingInfoResponse>> Handle(UpdateTrackingInfoCommand request, CancellationToken cancellationToken)
    {
        var shipment = await shipmentRepository.GetByIdAsync(request.ShipmentId, cancellationToken);
        if (shipment is null || shipment.TenantId != currentUser.TenantId)
            return Error.NotFound("Shipment.NotFound", $"Shipment {request.ShipmentId} not found.");

        shipment.UpdateTracking(request.NewTrackingNumber);
        shipmentRepository.Update(shipment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateTrackingInfoResponse(shipment.Id, shipment.TrackingNumber);
    }
}
