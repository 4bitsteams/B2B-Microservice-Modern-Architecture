using Microsoft.EntityFrameworkCore;
using B2B.Discount.Application.Interfaces;
using B2B.Shared.Infrastructure.Persistence;
using DiscountEntity = B2B.Discount.Domain.Entities.Discount;

namespace B2B.Discount.Infrastructure.Persistence.Repositories;

public sealed class DiscountRepository(DiscountDbContext context)
    : BaseRepository<DiscountEntity, Guid, DiscountDbContext>(context), IDiscountRepository
{
    public async Task<IReadOnlyList<DiscountEntity>> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await DbSet
            .Where(d => d.TenantId == tenantId && d.IsActive &&
                (!d.StartDate.HasValue || d.StartDate.Value <= now) &&
                (!d.EndDate.HasValue || d.EndDate.Value > now))
            .ToListAsync(ct);
    }
}
