using B2B.Order.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Order.Application.Commands.ShipOrder;

/// <summary>
/// Handles <see cref="ShipOrderCommand"/> by transitioning an order from
/// <c>Processing</c> to <c>Shipped</c> and recording the carrier tracking number.
///
/// <para>
/// Guard conditions:
/// <list type="bullet">
///   <item>
///     <description>
///       Order not found or belongs to a different tenant → <c>Error.NotFound</c>.
///     </description>
///   </item>
///   <item>
///     <description>
///       Order is not in <c>Processing</c> status → <c>Error.Validation</c>
///       (enforced by <c>Order.Ship</c> on the aggregate).
///     </description>
///   </item>
///   <item>
///     <description>
///       Concurrent modification detected → <c>Error.Conflict</c>; client must
///       refresh and retry.
///     </description>
///   </item>
/// </list>
/// </para>
///
/// <para>
/// On success, raises <c>OrderShippedEvent</c> (via <c>DomainEventBehavior</c>),
/// which triggers a shipment-tracking notification email to the customer.
/// </para>
/// </summary>
public sealed class ShipOrderHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<ShipOrderCommand>
{
    /// <inheritdoc/>
    public async Task<Result> Handle(ShipOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);

        if (order is null || order.TenantId != currentUser.TenantId)
            return Error.NotFound("Order.NotFound", $"Order {request.OrderId} not found.");

        var result = order.Ship(request.TrackingNumber);
        if (result.IsFailure)
            return result.Error;

        try
        {
            orderRepository.Update(order);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return Error.Conflict(
                "Order.ConcurrentModification",
                "The order was modified by another request. Please refresh and try again.");
        }

        return Result.Success();
    }
}
