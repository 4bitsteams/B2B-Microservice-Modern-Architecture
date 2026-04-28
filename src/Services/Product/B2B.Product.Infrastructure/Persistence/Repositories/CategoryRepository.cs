using Microsoft.EntityFrameworkCore;
using B2B.Product.Application.Interfaces;
using B2B.Product.Domain.Entities;
using B2B.Shared.Infrastructure.Persistence;

namespace B2B.Product.Infrastructure.Persistence.Repositories;

public sealed class CategoryRepository(ProductDbContext context)
    : BaseRepository<Category, Guid, ProductDbContext>(context), ICategoryRepository
{
    public async Task<Category?> GetBySlugAsync(string slug, Guid tenantId, CancellationToken ct = default) =>
        await DbSet.FirstOrDefaultAsync(c => c.Slug == slug && c.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<Category>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        await DbSet
            .Include(c => c.SubCategories)
            .Where(c => c.TenantId == tenantId && c.ParentCategoryId == null)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);
}
