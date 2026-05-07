using B2B.Order.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Order.Application.Commands.CancelOrder;

/// <summary>
/// Handles <see cref="CancelOrderCommand"/> by transitioning an order from any
/// non-terminal status to <c>Cancelled</c>.
///
/// <para>
/// Guard conditions:
/// <list type="bullet">
///   <item>
///     <description>
///       Order not found or belongs to a different tenant → <c>Error.NotFound</c>
///       (cross-tenant existence is never revealed).
///     </description>
///   </item>
///   <item>
///     <description>
///       Order is already <c>Delivered</c> or <c>Cancelled</c> → <c>Error.Validation</c>
///       (enforced by <c>Order.Cancel</c> on the aggregate).
///     </description>
///   </item>
///   <item>
///     <description>
///       Concurrent modification detected → <c>Error.Conflict</c>; client must
///       refresh and retry (requires EF optimistic concurrency token on the entity).
///     </description>
///   </item>
/// </list>
/// </para>
///
/// <para>
/// Authorization is checked by <see cref="CancelOrderAuthorizer"/> before this
/// handler runs: TenantAdmin / SuperAdmin can cancel any order; regular users
/// can only cancel their own.
/// </para>
/// </summary>
public sealed class CancelOrderHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<CancelOrderCommand>
{
    /// <inheritdoc/>
    public async Task<Result> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);

        if (order is null || order.TenantId != currentUser.TenantId)
            return Error.NotFound("Order.NotFound", $"Order {request.OrderId} not found.");

        var result = order.Cancel(request.Reason);
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
