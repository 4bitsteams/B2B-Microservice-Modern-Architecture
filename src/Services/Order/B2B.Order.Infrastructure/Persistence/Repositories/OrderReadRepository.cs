using Microsoft.EntityFrameworkCore;
using B2B.Order.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Infrastructure.Persistence;
using OrderEntity = B2B.Order.Domain.Entities.Order;
using OrderStatus = B2B.Order.Domain.Entities.OrderStatus;

namespace B2B.Order.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only repository — uses IDbContextFactory&lt;OrderDbContext&gt; (read replica,
/// QueryTrackingBehavior.NoTracking). Each method creates and immediately disposes
/// its own context; no entity is ever attached to a long-lived tracker.
/// </summary>
public sealed class OrderReadRepository(IDbContextFactory<OrderDbContext> factory)
    : BaseReadRepository<OrderEntity, Guid, OrderDbContext>(factory), IReadOrderRepository
{
    public async Task<OrderEntity?> GetByOrderNumberAsync(
        string orderNumber, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        return await ctx.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);
    }

    public async Task<OrderEntity?> GetWithItemsAsync(Guid id, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        return await ctx.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<PagedList<OrderEntity>> GetPagedByCustomerAsync(
        Guid customerId, Guid tenantId, int page, int pageSize,
        CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);

        var query = ctx.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId && o.TenantId == tenantId)
            .OrderByDescending(o => o.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedList<OrderEntity>.Create(items, page, pageSize, total);
    }

    public async Task<PagedList<OrderEntity>> GetPagedByTenantAsync(
        Guid tenantId, int page, int pageSize,
        OrderStatus? status = null, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);

        var query = ctx.Orders
            .Include(o => o.Items)
            .Where(o => o.TenantId == tenantId);

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        query = query.OrderByDescending(o => o.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedList<OrderEntity>.Create(items, page, pageSize, total);
    }
}
