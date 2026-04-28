using B2B.Order.Application.Interfaces;
using B2B.Shared.Core.Interfaces;
using OrderEntity = B2B.Order.Domain.Entities.Order;

namespace B2B.Order.Application.Commands.CancelOrder;

/// <summary>
/// Resource-based authorizer for <see cref="CancelOrderCommand"/>.
///
/// Rules:
///   • TenantAdmin and SuperAdmin can cancel any order in their tenant.
///   • A regular user can cancel only their own orders.
///   • Orders belonging to a different tenant are treated as not found (let the
///     handler return NotFound — do not leak existence information).
/// </summary>
public sealed class CancelOrderAuthorizer(
    IOrderRepository orderRepository,
    ICurrentUser currentUser) : IAuthorizer<CancelOrderCommand>
{
    public async Task<AuthorizationResult> AuthorizeAsync(
        CancelOrderCommand request, CancellationToken ct = default)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, ct);

        // Unknown order or cross-tenant: pass through so the handler returns a
        // consistent NotFound — do not expose whether the order exists.
        if (order is null || order.TenantId != currentUser.TenantId)
            return AuthorizationResult.Success();

        var isAdmin = currentUser.IsInRole("TenantAdmin") || currentUser.IsInRole("SuperAdmin");
        if (isAdmin || order.CustomerId == currentUser.UserId)
            return AuthorizationResult.Success();

        return AuthorizationResult.Fail("You do not have permission to cancel this order.");
    }
}
