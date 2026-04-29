using Microsoft.EntityFrameworkCore;
using B2B.Discount.Application.Interfaces;
using B2B.Discount.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Infrastructure.Persistence;

namespace B2B.Discount.Infrastructure.Persistence.Repositories;

public sealed class CouponReadRepository(IDbContextFactory<DiscountDbContext> factory)
    : BaseReadRepository<Coupon, Guid, DiscountDbContext>(factory), IReadCouponRepository
{
    public async Task<PagedList<Coupon>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        var query = ctx.Coupons.Where(c => c.TenantId == tenantId).OrderByDescending(c => c.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return PagedList<Coupon>.Create(items, page, pageSize, total);
    }
}
