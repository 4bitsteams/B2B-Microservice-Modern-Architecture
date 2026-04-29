using Microsoft.EntityFrameworkCore;
using B2B.Shipping.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Infrastructure.Persistence;
using ShipmentEntity = B2B.Shipping.Domain.Entities.Shipment;

namespace B2B.Shipping.Infrastructure.Persistence.Repositories;

public sealed class ShipmentReadRepository(IDbContextFactory<ShipmentDbContext> factory)
    : BaseReadRepository<ShipmentEntity, Guid, ShipmentDbContext>(factory), IReadShipmentRepository
{
    public async Task<ShipmentEntity?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        return await ctx.Shipments.FirstOrDefaultAsync(s => s.OrderId == orderId, ct);
    }

    public async Task<PagedList<ShipmentEntity>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        var query = ctx.Shipments.Where(s => s.TenantId == tenantId).OrderByDescending(s => s.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return PagedList<ShipmentEntity>.Create(items, page, pageSize, total);
    }
}
