using Microsoft.EntityFrameworkCore;
using B2B.Shipping.Application.Interfaces;
using B2B.Shared.Infrastructure.Persistence;
using ShipmentEntity = B2B.Shipping.Domain.Entities.Shipment;

namespace B2B.Shipping.Infrastructure.Persistence.Repositories;

public sealed class ShipmentRepository(ShipmentDbContext context)
    : BaseRepository<ShipmentEntity, Guid, ShipmentDbContext>(context), IShipmentRepository
{
    public async Task<ShipmentEntity?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default) =>
        await DbSet.FirstOrDefaultAsync(s => s.OrderId == orderId, ct);

    public async Task<ShipmentEntity?> GetByTrackingNumberAsync(string trackingNumber, CancellationToken ct = default) =>
        await DbSet.FirstOrDefaultAsync(s => s.TrackingNumber == trackingNumber, ct);
}
