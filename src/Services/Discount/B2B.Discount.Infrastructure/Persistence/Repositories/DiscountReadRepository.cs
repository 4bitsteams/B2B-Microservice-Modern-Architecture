using Microsoft.EntityFrameworkCore;
using B2B.Discount.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Infrastructure.Persistence;
using DiscountEntity = B2B.Discount.Domain.Entities.Discount;

namespace B2B.Discount.Infrastructure.Persistence.Repositories;

public sealed class DiscountReadRepository(IDbContextFactory<DiscountDbContext> factory)
    : BaseReadRepository<DiscountEntity, Guid, DiscountDbContext>(factory), IReadDiscountRepository
{
    public async Task<PagedList<DiscountEntity>> GetPagedByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        await using var ctx = await Factory.CreateDbContextAsync(ct);
        var query = ctx.Discounts.Where(d => d.TenantId == tenantId).OrderByDescending(d => d.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return PagedList<DiscountEntity>.Create(items, page, pageSize, total);
    }
}
