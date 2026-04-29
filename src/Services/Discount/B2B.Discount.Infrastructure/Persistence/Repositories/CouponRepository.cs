using Microsoft.EntityFrameworkCore;
using B2B.Discount.Application.Interfaces;
using B2B.Discount.Domain.Entities;
using B2B.Shared.Infrastructure.Persistence;

namespace B2B.Discount.Infrastructure.Persistence.Repositories;

public sealed class CouponRepository(DiscountDbContext context)
    : BaseRepository<Coupon, Guid, DiscountDbContext>(context), ICouponRepository
{
    public async Task<Coupon?> GetByCodeAsync(string code, Guid tenantId, CancellationToken ct = default) =>
        await DbSet.FirstOrDefaultAsync(c => c.Code == code.ToUpperInvariant() && c.TenantId == tenantId, ct);
}
