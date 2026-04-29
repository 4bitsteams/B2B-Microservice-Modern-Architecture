using B2B.Shipping.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shipping.Application.Commands.DispatchShipment;

public sealed class DispatchShipmentHandler(
    IShipmentRepository shipmentRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DispatchShipmentCommand>
{
    public async Task<Result> Handle(DispatchShipmentCommand request, CancellationToken cancellationToken)
    {
        var shipment = await shipmentRepository.GetByIdAsync(request.ShipmentId, cancellationToken);
        if (shipment is null || shipment.TenantId != currentUser.TenantId)
            return Error.NotFound("Shipment.NotFound", $"Shipment {request.ShipmentId} not found.");

        try { shipment.Ship(); }
        catch (InvalidOperationException ex)
        { return Error.Validation("Shipment.InvalidStatus", ex.Message); }

        shipmentRepository.Update(shipment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
