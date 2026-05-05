using B2B.Order.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Order.Application.Commands.ShipOrder;

public sealed class ShipOrderHandler(
    IOrderRepository orderRepository,  // write — primary DB
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<ShipOrderCommand>
{
    public async Task<Result> Handle(ShipOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);

        if (order is null || order.TenantId != currentUser.TenantId)
            return Error.NotFound("Order.NotFound", $"Order {request.OrderId} not found.");

        var result = order.Ship(request.TrackingNumber);
        if (result.IsFailure)
            return result.Error;

        orderRepository.Update(order);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
