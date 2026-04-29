using B2B.Shipping.Application.Interfaces;
using B2B.Shipping.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shipping.Application.Commands.CreateShipment;

public sealed class CreateShipmentHandler(
    IShipmentRepository shipmentRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateShipmentCommand, CreateShipmentResponse>
{
    public async Task<Result<CreateShipmentResponse>> Handle(CreateShipmentCommand request, CancellationToken cancellationToken)
    {
        var existing = await shipmentRepository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (existing is not null)
            return Error.Conflict("Shipment.AlreadyExists", $"A shipment for order {request.OrderId} already exists.");

        var shipment = Shipment.Create(
            request.OrderId, currentUser.TenantId,
            request.Carrier, request.RecipientName,
            request.Address, request.City, request.Country,
            request.ShippingCost, request.EstimatedDelivery);

        await shipmentRepository.AddAsync(shipment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateShipmentResponse(shipment.Id, shipment.TrackingNumber, shipment.Status.ToString());
    }
}
