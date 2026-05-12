using B2B.Web.Models.Common;
using B2B.Web.Models.Orders;

namespace B2B.Web.Services.Orders;

public interface IOrderService
{
    Task<PagedResult<OrderSummaryDto>?> GetOrdersAsync(
        int page = 1, int pageSize = 20, string? status = null);

    Task<OrderDto?> GetOrderAsync(Guid id);
    Task<OrderDto?> CreateOrderAsync(CreateOrderRequest request);
    Task<bool> ConfirmOrderAsync(Guid id);
    Task<bool> CancelOrderAsync(Guid id, string reason);
    Task<bool> ShipOrderAsync(Guid id, ShipOrderRequest request);
    Task<bool> DeliverOrderAsync(Guid id);
}
