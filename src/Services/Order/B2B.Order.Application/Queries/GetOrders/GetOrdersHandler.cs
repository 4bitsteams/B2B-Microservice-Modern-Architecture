using B2B.Order.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Order.Application.Queries.GetOrders;

public sealed class GetOrdersHandler(
    IReadOrderRepository orderRepository,   // read replica — NoTracking
    ICurrentUser currentUser)
    : IQueryHandler<GetOrdersQuery, PagedList<OrderSummaryDto>>
{
    public async Task<Result<PagedList<OrderSummaryDto>>> Handle(
        GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var isAdmin = currentUser.IsInRole("TenantAdmin") || currentUser.IsInRole("SuperAdmin");

        var orders = isAdmin
            ? await orderRepository.GetPagedByTenantAsync(
                currentUser.TenantId, request.Page, request.PageSize, request.Status, cancellationToken)
            : await orderRepository.GetPagedByCustomerAsync(
                currentUser.UserId, currentUser.TenantId, request.Page, request.PageSize, cancellationToken);

        var result = orders.Map(o => new OrderSummaryDto(
            o.Id, o.OrderNumber, o.Status.ToString(),
            o.TotalAmount, o.ItemCount, o.CreatedAt, o.ShippedAt));

        return result;
    }
}
