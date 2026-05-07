using B2B.Order.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Order.Application.Commands.DeliverOrder;

/// <summary>
/// Handles <see cref="DeliverOrderCommand"/> by transitioning an order from
/// <c>Shipped</c> to <c>Delivered</c> and recording the delivery timestamp.
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
///       Order is not in <c>Shipped</c> status → <c>Error.Validation</c>
///       (enforced by <c>Order.Deliver</c> on the aggregate).
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
/// <c>Delivered</c> is a terminal state — no further transitions are permitted.
/// On success, raises <c>OrderDeliveredEvent</c> (via <c>DomainEventBehavior</c>),
/// which triggers a delivery-confirmation notification to the customer.
/// </para>
/// </summary>
public sealed class DeliverOrderHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<DeliverOrderCommand>
{
    /// <inheritdoc/>
    public async Task<Result> Handle(DeliverOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);

        if (order is null || order.TenantId != currentUser.TenantId)
            return Error.NotFound("Order.NotFound", $"Order {request.OrderId} not found.");

        var result = order.Deliver();
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
