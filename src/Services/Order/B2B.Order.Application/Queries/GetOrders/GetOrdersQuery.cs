using B2B.Order.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;

namespace B2B.Order.Application.Queries.GetOrders;

public sealed record GetOrdersQuery(
    int Page = 1,
    int PageSize = 20,
    OrderStatus? Status = null) : IQuery<PagedList<OrderSummaryDto>>;

public sealed record OrderSummaryDto(
    Guid OrderId,
    string OrderNumber,
    string Status,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt,
    DateTime? ShippedAt);
