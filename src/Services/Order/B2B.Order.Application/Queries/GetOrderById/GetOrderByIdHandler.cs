using B2B.Order.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;
using OrderEntity = B2B.Order.Domain.Entities.Order;

namespace B2B.Order.Application.Queries.GetOrderById;

public sealed class GetOrderByIdHandler(
    IReadOrderRepository orderRepository,  // read replica — NoTracking
    ICurrentUser currentUser)
    : IQueryHandler<GetOrderByIdQuery, OrderDetailDto>
{
    public async Task<Result<OrderDetailDto>> Handle(
        GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetWithItemsAsync(request.OrderId, cancellationToken);

        if (order is null || order.TenantId != currentUser.TenantId)
            return Error.NotFound("Order.NotFound", $"Order {request.OrderId} not found.");

        // Customers may only view their own orders; admins may view any order in their tenant.
        var isAdmin = currentUser.IsInRole("TenantAdmin") || currentUser.IsInRole("SuperAdmin");
        if (!isAdmin && order.CustomerId != currentUser.UserId)
            return Error.Forbidden("Order.Forbidden", "You do not have permission to view this order.");

        return MapToDto(order);
    }

    private static OrderDetailDto MapToDto(OrderEntity order) => new(
        order.Id,
        order.OrderNumber,
        order.Status.ToString(),
        order.Subtotal,
        order.TaxAmount,
        order.ShippingCost,
        order.TotalAmount,
        order.ItemCount,
        new AddressDetailDto(
            order.ShippingAddress.Street,
            order.ShippingAddress.City,
            order.ShippingAddress.State,
            order.ShippingAddress.PostalCode,
            order.ShippingAddress.Country),
        order.BillingAddress is null ? null : new AddressDetailDto(
            order.BillingAddress.Street,
            order.BillingAddress.City,
            order.BillingAddress.State,
            order.BillingAddress.PostalCode,
            order.BillingAddress.Country),
        order.Notes,
        order.TrackingNumber,
        order.Items.Select(i => new OrderItemDetailDto(
            i.ProductId, i.ProductName, i.Sku, i.UnitPrice, i.Quantity, i.TotalPrice)).ToList(),
        order.CreatedAt,
        order.ShippedAt,
        order.DeliveredAt,
        order.CancelledAt,
        order.CancellationReason);
}
