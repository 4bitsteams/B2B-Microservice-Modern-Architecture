using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using ShipmentEntity = B2B.Shipping.Domain.Entities.Shipment;

namespace B2B.Shipping.Application.Interfaces;

public interface IShipmentRepository : IRepository<ShipmentEntity, Guid>
{
    Task<ShipmentEntity?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<ShipmentEntity?> GetByTrackingNumberAsync(string trackingNumber, CancellationToken ct = default);
}

public interface IReadShipmentRepository : IReadRepository<ShipmentEntity, Guid>
{
    Task<ShipmentEntity?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<PagedList<ShipmentEntity>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
}
