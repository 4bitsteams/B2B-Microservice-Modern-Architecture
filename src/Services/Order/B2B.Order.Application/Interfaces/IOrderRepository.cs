using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using OrderEntity = B2B.Order.Domain.Entities.Order;
using OrderStatus = B2B.Order.Domain.Entities.OrderStatus;

namespace B2B.Order.Application.Interfaces;

/// <summary>Write repository — targets the primary DB with change tracking.</summary>
public interface IOrderRepository : IRepository<OrderEntity, Guid>;

/// <summary>
/// Read-only repository — targets the read replica with NoTracking.
/// Inject into query handlers only; never call SaveChanges on derived contexts.
/// </summary>
public interface IReadOrderRepository : IReadRepository<OrderEntity, Guid>
{
    Task<OrderEntity?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default);
    Task<OrderEntity?> GetWithItemsAsync(Guid id, CancellationToken ct = default);
    Task<PagedList<OrderEntity>> GetPagedByCustomerAsync(Guid customerId, Guid tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<PagedList<OrderEntity>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, OrderStatus? status = null, CancellationToken ct = default);
}
