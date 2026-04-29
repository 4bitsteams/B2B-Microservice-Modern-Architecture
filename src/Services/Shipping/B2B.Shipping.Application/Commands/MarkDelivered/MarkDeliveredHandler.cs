using B2B.Shipping.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shipping.Application.Commands.MarkDelivered;

public sealed class MarkDeliveredHandler(
    IShipmentRepository shipmentRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<MarkDeliveredCommand>
{
    public async Task<Result> Handle(MarkDeliveredCommand request, CancellationToken cancellationToken)
    {
        var shipment = await shipmentRepository.GetByIdAsync(request.ShipmentId, cancellationToken);
        if (shipment is null || shipment.TenantId != currentUser.TenantId)
            return Error.NotFound("Shipment.NotFound", $"Shipment {request.ShipmentId} not found.");

        try { shipment.MarkDelivered(); }
        catch (InvalidOperationException ex)
        { return Error.Validation("Shipment.InvalidStatus", ex.Message); }

        shipmentRepository.Update(shipment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
