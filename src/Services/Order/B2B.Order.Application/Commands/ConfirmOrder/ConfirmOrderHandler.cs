using B2B.Order.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Order.Application.Commands.ConfirmOrder;

/// <summary>
/// Handles <see cref="ConfirmOrderCommand"/> by transitioning an order from
/// <c>Pending</c> to <c>Confirmed</c>.
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
///       Order is not in <c>Pending</c> status → <c>Error.Validation</c>
///       (enforced by <c>Order.Confirm</c> on the aggregate).
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
/// On success, raises <c>OrderConfirmedEvent</c> (via <c>DomainEventBehavior</c>),
/// which starts the <c>OrderFulfillmentSaga</c>.
/// </para>
/// </summary>
public sealed class ConfirmOrderHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<ConfirmOrderCommand>
{
    /// <inheritdoc/>
    public async Task<Result> Handle(ConfirmOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);

        if (order is null || order.TenantId != currentUser.TenantId)
            return Error.NotFound("Order.NotFound", $"Order {request.OrderId} not found.");

        var result = order.Confirm();
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
